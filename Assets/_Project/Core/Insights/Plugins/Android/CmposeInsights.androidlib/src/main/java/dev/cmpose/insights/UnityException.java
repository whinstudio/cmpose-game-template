package dev.cmpose.insights;

// A C# exception as Crashlytics shows it: "<C# type>: <message>" with the C# frames as its stack trace
public final class UnityException extends RuntimeException {
    private static final long serialVersionUID = 1L;

    public UnityException(final String message) {
        super(message);
    }
}
