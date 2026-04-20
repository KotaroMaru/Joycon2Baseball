#import <CoreBluetooth/CoreBluetooth.h>
#import <Foundation/Foundation.h>
#include <vector>
#include <string>

#pragma pack(push, 1)
struct JoyconData {
    int deviceID;
    unsigned int buttons;
    short stickX, stickY;
    short accelX, accelY, accelZ;
    short gyroX, gyroY, gyroZ;
    short mouseX, mouseY;
    unsigned char trigger;
};
#pragma pack(pop)

typedef void (*JoyconDataCallback)(JoyconData);
typedef void (*JoyconLogCallback)(const char*);
static JoyconDataCallback s_dataCallback = NULL;
static JoyconLogCallback s_logCallback = NULL;

void UnityLog(const char* msg) {
    NSLog(@"Joycon2macOS[v10]: %s", msg);
    if (s_logCallback) s_logCallback(msg);
}

@interface Joycon2macOS : NSObject <CBCentralManagerDelegate, CBPeripheralDelegate>
@property (strong, nonatomic) CBCentralManager* centralManager;
@property (strong, nonatomic) NSMutableDictionary<NSUUID*, CBPeripheral*>* peripherals;
@property (strong, nonatomic) NSMutableDictionary<NSUUID*, CBCharacteristic*>* writeChars;
@property (strong, nonatomic) NSMutableDictionary<NSUUID*, CBCharacteristic*>* subChars;
@property (strong, nonatomic) NSMutableSet<NSUUID*>* initializedPeripherals;
@property (strong, nonatomic) NSMutableSet<NSUUID*>* highRatePeripherals;
@property (assign, nonatomic) BOOL shouldScan;
@end

@implementation Joycon2macOS

+ (instancetype)sharedInstance {
    static Joycon2macOS* instance = nil;
    static dispatch_once_t onceToken;
    dispatch_once(&onceToken, ^{
        instance = [[Joycon2macOS alloc] init];
    });
    return instance;
}

- (instancetype)init {
    self = [super init];
    if (self) {
        _centralManager = [[CBCentralManager alloc] initWithDelegate:self queue:nil];
        _peripherals = [[NSMutableDictionary alloc] init];
        _writeChars = [[NSMutableDictionary alloc] init];
        _subChars = [[NSMutableDictionary alloc] init];
        _initializedPeripherals = [[NSMutableSet alloc] init];
        _highRatePeripherals = [[NSMutableSet alloc] init];
        _shouldScan = NO;
    }
    return self;
}

- (void)startScan {
    _shouldScan = YES;
    if (_centralManager.state == CBManagerStatePoweredOn) {
        [_centralManager scanForPeripheralsWithServices:nil options:nil];
        UnityLog("SECTION: ------ Scanning BLE devices ------");
    }
}

- (void)stopScan {
    _shouldScan = NO;
    [_centralManager stopScan];
}

- (void)centralManagerDidUpdateState:(CBCentralManager *)central {
    if (central.state == CBManagerStatePoweredOn && _shouldScan) [self startScan];
}

- (void)centralManager:(CBCentralManager *)central didDiscoverPeripheral:(CBPeripheral *)peripheral advertisementData:(NSDictionary<NSString *,id> *)advertisementData RSSI:(NSNumber *)RSSI {
    NSData* manufacturerData = advertisementData[CBAdvertisementDataManufacturerDataKey];
    if (manufacturerData && manufacturerData.length >= 2) {
        uint16_t companyId;
        [manufacturerData getBytes:&companyId length:2];
        if (companyId == 0x0553) {
            UnityLog([[NSString stringWithFormat:@"INFO: Joy-Con found: %@", peripheral.name] UTF8String]);
            _peripherals[peripheral.identifier] = peripheral;
            NSDictionary* options = @{
                CBConnectPeripheralOptionNotifyOnConnectionKey: @YES,
                CBConnectPeripheralOptionNotifyOnDisconnectionKey: @YES,
                CBConnectPeripheralOptionNotifyOnNotificationKey: @YES,
                CBConnectPeripheralOptionStartDelayKey: @0
            };
            [central connectPeripheral:peripheral options:options];
        }
    }
}

- (void)centralManager:(CBCentralManager *)central didConnectPeripheral:(CBPeripheral *)peripheral {
    UnityLog("SECTION: ------ Connection Established ------");
    peripheral.delegate = self;
    [peripheral discoverServices:nil];
}

- (void)centralManager:(CBCentralManager *)central didDisconnectPeripheral:(CBPeripheral *)peripheral error:(NSError *)error {
    UnityLog([[NSString stringWithFormat:@"INFO: 🔌 Disconnected from %@", peripheral.name] UTF8String]);
    [_peripherals removeObjectForKey:peripheral.identifier];
    [_writeChars removeObjectForKey:peripheral.identifier];
    [_subChars removeObjectForKey:peripheral.identifier];
    [_initializedPeripherals removeObject:peripheral.identifier];
    [_highRatePeripherals removeObject:peripheral.identifier];
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverServices:(NSError *)error {
    if (error) return;
    for (CBService* service in peripheral.services) {
        [peripheral discoverCharacteristics:nil forService:service];
    }
}

- (void)peripheral:(CBPeripheral *)peripheral didDiscoverCharacteristicsForService:(CBService *)service error:(NSError *)error {
    if (error) return;
    NSString* WRITE_UUID = @"649D4AC9-8EB7-4E6C-AF44-1EA54FE5F005";
    NSString* SUB_UUID = @"AB7DE9BE-89FE-49AD-828F-118F09DF7FD2";
    for (CBCharacteristic* c in service.characteristics) {
        if ([c.UUID.UUIDString isEqualToString:WRITE_UUID]) {
            _writeChars[peripheral.identifier] = c;
            UnityLog("INFO: Found WRITE characteristic");
        } else if ([c.UUID.UUIDString isEqualToString:SUB_UUID]) {
            _subChars[peripheral.identifier] = c;
            UnityLog("INFO: Found SUBSCRIBE characteristic, enabling notifications...");
            [peripheral setNotifyValue:YES forCharacteristic:c];
        }
    }
    CBCharacteristic* w = _writeChars[peripheral.identifier];
    CBCharacteristic* s = _subChars[peripheral.identifier];
    if (w && s && ![_initializedPeripherals containsObject:peripheral.identifier]) {
        [_initializedPeripherals addObject:peripheral.identifier];
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(0.5 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            [self performHandshake:peripheral];
        });
    }
}

- (void)performHandshake:(CBPeripheral*)peripheral {
    CBCharacteristic* w = _writeChars[peripheral.identifier];
    CBCharacteristic* s = _subChars[peripheral.identifier];
    if (!w || !s) return;

    dispatch_async(dispatch_get_global_queue(DISPATCH_QUEUE_PRIORITY_DEFAULT, 0), ^{
        uint8_t cmd1[] = {0x0c, 0x91, 0x01, 0x02, 0x00, 0x04, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00};
        uint8_t cmd2[] = {0x0c, 0x91, 0x01, 0x04, 0x00, 0x04, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00};
        
        UnityLog("INFO: Sending Handshake Step 1 (Buttons)");
        [peripheral writeValue:[NSData dataWithBytes:cmd1 length:12] forCharacteristic:w type:CBCharacteristicWriteWithoutResponse];
        
        [NSThread sleepForTimeInterval:0.5];
        
        UnityLog("INFO: Sending Handshake Step 2 (IMU)");
        [peripheral writeValue:[NSData dataWithBytes:cmd2 length:12] forCharacteristic:w type:CBCharacteristicWriteWithoutResponse];
        
        dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(2.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
            UnityLog("INFO: Re-enabling notifications for stability...");
            [peripheral setNotifyValue:YES forCharacteristic:s];
            [self startRetryTimer:peripheral];
        });
    });
}

- (void)startRetryTimer:(CBPeripheral*)peripheral {
    dispatch_after(dispatch_time(DISPATCH_TIME_NOW, (int64_t)(3.0 * NSEC_PER_SEC)), dispatch_get_main_queue(), ^{
        if (peripheral.state == CBPeripheralStateConnected && ![_highRatePeripherals containsObject:peripheral.identifier]) {
            UnityLog("WARN: High-rate missing, retrying IMU command...");
            CBCharacteristic* w = self.writeChars[peripheral.identifier];
            if (w) {
                uint8_t cmd2[] = {0x0c, 0x91, 0x01, 0x04, 0x00, 0x04, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00};
                [peripheral writeValue:[NSData dataWithBytes:cmd2 length:12] forCharacteristic:w type:CBCharacteristicWriteWithoutResponse];
            }
            [self startRetryTimer:peripheral];
        }
    });
}

- (void)peripheral:(CBPeripheral *)peripheral didUpdateValueForCharacteristic:(CBCharacteristic *)characteristic error:(NSError *)error {
    if (error) return;
    if ([characteristic.UUID.UUIDString isEqualToString:@"AB7DE9BE-89FE-49AD-828F-118F09DF7FD2"]) {
        NSData* data = characteristic.value;
        const uint8_t* bytes = (const uint8_t*)data.bytes;
        if (data.length >= 7) {
            JoyconData jData = {0};
            jData.deviceID = (peripheral.name && ([peripheral.name containsString:@"(R)"] || [peripheral.name containsString:@"Right"])) ? 1 : 0;
            uint32_t b; memcpy(&b, &bytes[3], 4);
            jData.buttons = CFSwapInt32LittleToHost(b);
            if (data.length >= 60) {
                if (![_highRatePeripherals containsObject:peripheral.identifier]) {
                    [_highRatePeripherals addObject:peripheral.identifier];
                    UnityLog([[NSString stringWithFormat:@"DATA: High-rate received! Length: %lu", (unsigned long)data.length] UTF8String]);
                }
                
                // --- v5 STICK LOGIC (Left=0x0A, Right=0x0D, with -2048) ---
                size_t stickOffset = (jData.deviceID == 0) ? 0x0A : 0x0D;
                uint32_t s = 0; memcpy(&s, &bytes[stickOffset], 3);
                jData.stickX = (short)((s & 0xFFF) - 2048);
                jData.stickY = (short)(((s >> 12) & 0xFFF) - 2048);
                
                short v;
                memcpy(&v, &bytes[0x10], 2); jData.mouseX = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x12], 2); jData.mouseY = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x30], 2); jData.accelX = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x32], 2); jData.accelY = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x34], 2); jData.accelZ = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x36], 2); jData.gyroX = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x38], 2); jData.gyroY = CFSwapInt16LittleToHost(v);
                memcpy(&v, &bytes[0x3A], 2); jData.gyroZ = CFSwapInt16LittleToHost(v);
                
                if (data.length >= 62) {
                    jData.trigger = bytes[jData.deviceID == 0 ? 0x3C : 0x3D];
                }
            }
            if (s_dataCallback) s_dataCallback(jData);
        }
    }
}
@end

extern "C" {
    void UnityStartScan() { [[Joycon2macOS sharedInstance] startScan]; }
    void UnityStopScan() { [[Joycon2macOS sharedInstance] stopScan]; }
    void UnitySetDataCallback(JoyconDataCallback callback) { s_dataCallback = callback; }
    void UnitySetLogCallback(JoyconLogCallback callback) { s_logCallback = callback; }
}
