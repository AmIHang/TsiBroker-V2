using Xunit;

// These tests spin up real Kestrel hosts and loopback HTTP listeners; keeping them serialized
// avoids port/listener churn from many of these starting up concurrently. The suite is small
// enough (well under a second serialized) that parallelizing wouldn't meaningfully help anyway.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
