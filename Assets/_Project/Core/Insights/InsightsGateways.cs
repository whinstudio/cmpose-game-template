using Kturker.UnityBase.Core.Domain;

namespace Kturker.UnityBase.Core.Insights
{
    public static class InsightsGateways
    {
        // enabled is false in the editor and in automation runs: the SDK is never reached then
        public static IInsightsGateway Create(bool enabled, ILog log)
        {
            if (!enabled) return new NoInsightsGateway();
#if UNITY_ANDROID && !UNITY_EDITOR
            return new AndroidInsightsGateway(log);
#elif UNITY_IOS && !UNITY_EDITOR
            return new IosInsightsGateway(log);
#else
            return new NoInsightsGateway();
#endif
        }
    }
}
