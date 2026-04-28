namespace Posiflora.Recovery.Agent;

public class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var heartbeat = new AgentHeartbeat(Environment.MachineName, Environment.ProcessId);

        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation(
                "POS Radar Agent heartbeat: machine={MachineName} processId={ProcessId} time={Time}",
                heartbeat.MachineName,
                heartbeat.ProcessId,
                DateTimeOffset.Now);

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
