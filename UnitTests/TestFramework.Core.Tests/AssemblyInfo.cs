using Xunit;

// Collections run one at a time rather than in parallel.
//
// Several tests in this assembly assert scheduling behaviour - that an exclusive step acts as a
// barrier, that independent steps are not merged, that a consumer waits for its producer. They
// prove those properties by observing when a step actually starts, which makes them sensitive to
// how quickly the thread pool hands out threads.
//
// Running collections in parallel on a two-core CI runner starves that pool: the assertions then
// fail not because the ordering is wrong but because a continuation waited too long for a thread.
// Raising the timeouts does not fix it - a starved run can exceed any budget - so the parallelism
// itself has to go.
//
// The cost is small: the whole assembly runs in roughly a second, so there is little to win from
// overlapping it, and determinism on the runner is worth more than the difference.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
