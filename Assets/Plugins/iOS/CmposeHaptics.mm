#import <UIKit/UIKit.h>

// Called from IosVibrator (C#): 0 light, 1 medium, 2 heavy, matching HapticStrength
extern "C" void CmposeHapticImpact(int strength)
{
    UIImpactFeedbackStyle style = UIImpactFeedbackStyleLight;
    if (strength == 1) style = UIImpactFeedbackStyleMedium;
    else if (strength >= 2) style = UIImpactFeedbackStyleHeavy;
    UIImpactFeedbackGenerator *generator = [[UIImpactFeedbackGenerator alloc] initWithStyle:style];
    [generator prepare];
    [generator impactOccurred];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}

// Called from IosVibrator (C#): 0 success, 1 failure, matching HapticEvent
extern "C" void CmposeHapticNotify(int kind)
{
    UINotificationFeedbackType type = kind == 1 ? UINotificationFeedbackTypeError : UINotificationFeedbackTypeSuccess;
    UINotificationFeedbackGenerator *generator = [[UINotificationFeedbackGenerator alloc] init];
    [generator prepare];
    [generator notificationOccurred:type];
#if !__has_feature(objc_arc)
    [generator release];
#endif
}
