namespace Sampling
{
  internal sealed record FilteredSampleState(
    string OutputName,
    double From,
    double To,
    int At,
    bool IsEnabled
    );
}
