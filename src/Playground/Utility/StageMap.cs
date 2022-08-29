namespace Playground.Benchmark;

public class StageMap
{
    public IDictionary<string, Stage>
        Stages { get; } = new Dictionary<string, Stage>();

    public void Add(string name, Stage stage)
    {
        Stages.Add(name, stage);
    }

    public long TotalElapsedMilliseconds => 
        Stages.Values.Sum(x => x.ElapsedMilliseconds);
}
