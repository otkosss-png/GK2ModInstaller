using Xunit;

// LoaderText.Language is process-wide static state that several test classes flip.
// Run tests sequentially so classes never race on it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
