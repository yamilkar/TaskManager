// The CDK runs on the jsii runtime, which extracts its bundled assets to temp files on first use and cannot do that
// from two test classes at once.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
