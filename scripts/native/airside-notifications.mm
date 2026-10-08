// Original Airside bridge to macOS Notification Centre; built into the player's universal bundle.
#import <AppKit/AppKit.h>
#import <CoreServices/CoreServices.h>
#import <UserNotifications/UserNotifications.h>
#include <atomic>

// 0 not asked, 1 requesting, 2 allowed, 3 denied, 4 unavailable.
static std::atomic<int> permission(0);

@interface AirsideNotificationDelegate : NSObject <UNUserNotificationCenterDelegate>
@end
@implementation AirsideNotificationDelegate
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
       willPresentNotification:(UNNotification *)notification
         withCompletionHandler:(void (^)(UNNotificationPresentationOptions))completion {
    // Normal events stay in the game while focused. A deliberate test previews the real banner.
    BOOL test = [notification.request.content.userInfo[@"airsideTest"] boolValue];
    if (test || ![NSApp isActive]) {
        UNNotificationPresentationOptions options = UNNotificationPresentationOptionSound;
        if (@available(macOS 11.0, *)) options |= UNNotificationPresentationOptionBanner;
        else options |= UNNotificationPresentationOptionAlert;
        completion(options);
    } else completion(0);
}
- (void)userNotificationCenter:(UNUserNotificationCenter *)center
didReceiveNotificationResponse:(UNNotificationResponse *)response
         withCompletionHandler:(void (^)(void))completion {
    dispatch_async(dispatch_get_main_queue(), ^{
        [NSApp activateIgnoringOtherApps:YES];
        for (NSWindow *window in [NSApp windows]) {
            if ([window canBecomeMainWindow]) { [window deminiaturize:nil]; [window makeKeyAndOrderFront:nil]; break; }
        }
        completion();
    });
}
@end

static UNUserNotificationCenter *Center() {
    if (![[NSBundle mainBundle] bundleIdentifier]) {
        NSLog(@"Airside notifications unavailable: launch the built Airside.app with a bundle identifier.");
        permission.store(4); return nil;
    }
    static AirsideNotificationDelegate *delegate;
    static dispatch_once_t once;
    dispatch_once(&once, ^{ delegate = [AirsideNotificationDelegate new]; });
    UNUserNotificationCenter *center = [UNUserNotificationCenter currentNotificationCenter];
    center.delegate = delegate;
    return center;
}

extern "C" __attribute__((visibility("default"))) int AS_PermissionState() { return permission.load(); }

extern "C" __attribute__((visibility("default"))) void AS_RefreshPermission() {
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
            UNUserNotificationCenter *center = Center();
            if (!center) return;
            [center getNotificationSettingsWithCompletionHandler:^(UNNotificationSettings *settings) {
                switch (settings.authorizationStatus) {
                    case UNAuthorizationStatusAuthorized: permission.store(2); break;
                    case UNAuthorizationStatusProvisional: permission.store(2); break;
                    case UNAuthorizationStatusDenied: permission.store(3); break;
                    default:
                        // Keep a failed request visible until the player retries it.
                        if (permission.load() != 1 && permission.load() != 4) permission.store(0);
                        break;
                }
            }];
        } @catch (NSException *exception) {
            NSLog(@"Airside notifications failed: %@: %@", exception.name, exception.reason);
            permission.store(4);
        }
    });
}

extern "C" __attribute__((visibility("default"))) void AS_RequestPermission() {
    permission.store(1);
    dispatch_async(dispatch_get_main_queue(), ^{
        @try {
            // Register the actual player bundle even when launched directly from a build folder.
            // Keep the existing bundle identity so macOS permissions survive future builds.
            NSBundle *bundle = [NSBundle mainBundle];
            if (![bundle bundleIdentifier]) { Center(); return; }
            OSStatus registration = LSRegisterURL((__bridge CFURLRef)bundle.bundleURL, true);
            if (registration != noErr)
                NSLog(@"Airside notification app registration failed: %d (%@)", (int)registration, bundle.bundleIdentifier);
            UNUserNotificationCenter *center = Center();
            if (!center) return;
            [center requestAuthorizationWithOptions:(UNAuthorizationOptionAlert | UNAuthorizationOptionSound)
                                  completionHandler:^(BOOL granted, NSError *error) {
                if (error) NSLog(@"Airside notification permission failed: %@ (%ld): %@", error.domain, (long)error.code, error.localizedDescription);
                permission.store(error ? 4 : granted ? 2 : 3);
            }];
        } @catch (NSException *exception) {
            NSLog(@"Airside notifications failed: %@: %@", exception.name, exception.reason);
            permission.store(4);
        }
    });
}

extern "C" __attribute__((visibility("default"))) void AS_Send(const char *title, const char *body, int sound, int test) {
    if (!title || !body || permission.load() != 2) return;
    // Copy managed UTF-8 strings before dispatch; marshalled pointers expire when this call returns.
    NSString *heading = [NSString stringWithUTF8String:title];
    NSString *message = [NSString stringWithUTF8String:body];
    if (!heading || !message) return;
    dispatch_async(dispatch_get_main_queue(), ^{
        if (!test && [NSApp isActive]) return;
        @try {
            UNMutableNotificationContent *content = [UNMutableNotificationContent new];
            content.title = heading;
            content.body = message;
            content.threadIdentifier = @"airside.airline";
            content.userInfo = @{@"airsideTest": @(test != 0)};
            if (sound) content.sound = [UNNotificationSound defaultSound];
            UNNotificationRequest *request = [UNNotificationRequest requestWithIdentifier:[[NSUUID UUID] UUIDString]
                                                                                  content:content trigger:nil];
            [Center() addNotificationRequest:request withCompletionHandler:^(NSError *error) {
                if (error) NSLog(@"Airside notification delivery failed: %@", error.localizedDescription);
            }];
        } @catch (NSException *exception) {
            NSLog(@"Airside notifications failed: %@: %@", exception.name, exception.reason);
            permission.store(4);
        }
    });
}

extern "C" __attribute__((visibility("default"))) void AS_OpenSettings() {
    dispatch_async(dispatch_get_main_queue(), ^{
        [[NSWorkspace sharedWorkspace] openURL:[NSURL URLWithString:@"x-apple.systempreferences:com.apple.Notifications-Settings.extension"]];
    });
}
