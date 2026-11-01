#import <FirebaseCore/FirebaseCore.h>
#import <FirebaseCrashlytics/FirebaseCrashlytics.h>
#import <FirebaseAnalytics/FirebaseAnalytics.h>

// Firebase Crashlytics and Analytics for the game's C# code (IosInsightsGateway), called on Unity's main thread.
// CmposeInsightsStart configures Firebase only when GoogleService-Info.plist is in the app; every other call does
// nothing while Firebase is not configured.

static NSString* CmposeInsightsString(const char* value)
{
    if (value == NULL) return @"";
    return [NSString stringWithUTF8String:value] ?: @"";
}

static BOOL CmposeInsightsReady(void)
{
    return [FIRApp defaultApp] != nil;
}

// UnityFramework is built without Objective-C exceptions, so the plist is checked before configuring: FIRApp raises
// (and the game would crash at launch) when the app id or sender id is missing
extern "C" bool CmposeInsightsStart(void)
{
    if (CmposeInsightsReady()) return true;
    NSString* path = [[NSBundle mainBundle] pathForResource:@"GoogleService-Info" ofType:@"plist"];
    if (path == nil) return false;
    FIROptions* options = [[FIROptions alloc] initWithContentsOfFile:path];
    if (options.googleAppID.length == 0 || options.GCMSenderID.length == 0)
    {
        NSLog(@"Insights: GoogleService-Info.plist has no GOOGLE_APP_ID or GCM_SENDER_ID, Firebase not started");
        return false;
    }
    [FIRApp configureWithOptions:options];
    return CmposeInsightsReady();
}

extern "C" void CmposeInsightsSetAnalytics(bool enabled)
{
    if (!CmposeInsightsReady()) return;
    [FIRAnalytics setAnalyticsCollectionEnabled:enabled];
}

extern "C" void CmposeInsightsSetCrashlytics(bool enabled)
{
    if (!CmposeInsightsReady()) return;
    [[FIRCrashlytics crashlytics] setCrashlyticsCollectionEnabled:enabled];
}

// paramsJson is a JSON object of strings and numbers (IosInsightsGateway writes it)
extern "C" void CmposeInsightsLogEvent(const char* name, const char* paramsJson)
{
    if (!CmposeInsightsReady()) return;
    NSDictionary<NSString*, id>* parameters = nil;
    NSData* data = [CmposeInsightsString(paramsJson) dataUsingEncoding:NSUTF8StringEncoding];
    if (data.length > 0)
    {
        id parsed = [NSJSONSerialization JSONObjectWithData:data options:0 error:nil];
        if ([parsed isKindOfClass:[NSDictionary class]] && [parsed count] > 0) parameters = parsed;
    }
    [FIRAnalytics logEventWithName:CmposeInsightsString(name) parameters:parameters];
}

// NULL clears the id
extern "C" void CmposeInsightsSetUserId(const char* userId)
{
    if (!CmposeInsightsReady()) return;
    NSString* text = userId != NULL ? CmposeInsightsString(userId) : nil;
    [FIRAnalytics setUserID:text];
    [[FIRCrashlytics crashlytics] setUserID:text ?: @""];
}

extern "C" void CmposeInsightsLog(const char* message)
{
    if (!CmposeInsightsReady()) return;
    [[FIRCrashlytics crashlytics] log:CmposeInsightsString(message)];
}

// A non-fatal with the C# stack: framesTsv has one "symbol\tfile\tline" line per frame (file empty and line 0 when
// the trace has none)
extern "C" void CmposeInsightsRecord(const char* type, const char* message, const char* framesTsv)
{
    if (!CmposeInsightsReady()) return;
    NSMutableArray<FIRStackFrame*>* frames = [NSMutableArray array];
    for (NSString* line in [CmposeInsightsString(framesTsv) componentsSeparatedByString:@"\n"])
    {
        if (line.length == 0) continue;
        NSArray<NSString*>* parts = [line componentsSeparatedByString:@"\t"];
        NSString* symbol = parts[0];
        NSString* file = parts.count > 1 ? parts[1] : @"";
        NSInteger number = parts.count > 2 ? parts[2].integerValue : 0;
        [frames addObject:[FIRStackFrame stackFrameWithSymbol:symbol file:file line:number]];
    }
    FIRExceptionModel* model = [FIRExceptionModel exceptionModelWithName:CmposeInsightsString(type)
                                                                  reason:CmposeInsightsString(message)];
    model.stackTrace = frames;
    [[FIRCrashlytics crashlytics] recordExceptionModel:model];
}
