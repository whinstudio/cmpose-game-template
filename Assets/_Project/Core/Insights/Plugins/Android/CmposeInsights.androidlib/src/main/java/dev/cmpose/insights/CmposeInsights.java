package dev.cmpose.insights;

import android.content.Context;
import android.os.Bundle;
import com.google.firebase.FirebaseApp;
import com.google.firebase.analytics.FirebaseAnalytics;
import com.google.firebase.crashlytics.FirebaseCrashlytics;

// Firebase Crashlytics and Analytics for the game's C# code (AndroidInsightsGateway), called on Unity's main thread.
// start is the only call that may run without Firebase: it answers false when the build has no google-services
// resources, and the gateway then never calls the rest. Any other failure throws back to the gateway.
public final class CmposeInsights {
    private static Context context;

    private CmposeInsights() {
    }

    // True when FirebaseApp is set up (its init provider reads the resources at launch; started again here if not)
    public static boolean start(final Context activity) {
        try {
            final Context app = activity.getApplicationContext();
            if (FirebaseApp.getApps(app).isEmpty()) FirebaseApp.initializeApp(app);
            if (FirebaseApp.getApps(app).isEmpty()) return false;
            context = app;
            return true;
        } catch (RuntimeException e) {
            return false;
        }
    }

    public static void setAnalyticsEnabled(final boolean enabled) {
        FirebaseAnalytics.getInstance(context).setAnalyticsCollectionEnabled(enabled);
    }

    public static void setCrashlyticsEnabled(final boolean enabled) {
        FirebaseCrashlytics.getInstance().setCrashlyticsCollectionEnabled(enabled);
    }

    public static void logEvent(final String name, final Bundle parameters) {
        FirebaseAnalytics.getInstance(context).logEvent(name, parameters);
    }

    // null clears the id
    public static void setUserId(final String id) {
        FirebaseAnalytics.getInstance(context).setUserId(id);
        FirebaseCrashlytics.getInstance().setUserId(id == null ? "" : id);
    }

    public static void log(final String message) {
        FirebaseCrashlytics.getInstance().log(message);
    }

    // A non-fatal with the C# stack: one frame per symbol, files null and lines 0 when the trace has none
    public static void recordException(final String type, final String message, final String[] symbols,
            final String[] files, final int[] lines) {
        final StackTraceElement[] stack = new StackTraceElement[symbols.length];
        for (int i = 0; i < symbols.length; i++) {
            final String[] parts = split(symbols[i]);
            stack[i] = new StackTraceElement(parts[0], parts[1], files[i], lines[i]);
        }
        final UnityException exception = new UnityException(type + ": " + message);
        exception.setStackTrace(stack);
        FirebaseCrashlytics.getInstance().recordException(exception);
    }

    // "Ns.Type:Method (args)" (Unity) or "Ns.Type.Method (args)" (.NET) into declaring class and method
    static String[] split(final String symbol) {
        String name = symbol == null ? "" : symbol;
        final int args = name.indexOf('(');
        if (args > 0) name = name.substring(0, args);
        name = name.trim();
        int at = name.lastIndexOf(':');
        if (at < 0) at = name.lastIndexOf('.');
        if (at <= 0 || at == name.length() - 1) return new String[] {"", name};
        return new String[] {name.substring(0, at), name.substring(at + 1)};
    }
}
