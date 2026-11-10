namespace TaskManager.SharedKernel;

public interface IAppMetrics
{
    void Increment(string metricName);
}
