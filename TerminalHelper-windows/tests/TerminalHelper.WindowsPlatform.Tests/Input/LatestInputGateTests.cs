using TerminalHelper.Windows.Input;

namespace TerminalHelper.WindowsPlatform.Tests.Input;

[TestClass]
public sealed class LatestInputGateTests
{
    [TestMethod]
    public async Task TryApply_ReverseCompletionKeepsOnlyTheNewestInput()
    {
        var gate = new LatestInputGate();
        var firstRead = new TaskCompletionSource<IReadOnlyList<string?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondRead = new TaskCompletionSource<IReadOnlyList<string?>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var applied = new List<string[]>();

        var first = ReadAndApplyAsync(firstRead.Task);
        var second = ReadAndApplyAsync(secondRead.Task);

        secondRead.SetResult(["second"]);
        await second;
        firstRead.SetResult(["first"]);
        await first;

        Assert.AreEqual(1, applied.Count);
        CollectionAssert.AreEqual(new[] { "second" }, applied[0]);

        async Task ReadAndApplyAsync(Task<IReadOnlyList<string?>> read)
        {
            var generation = gate.BeginInput();
            var paths = await read;
            gate.TryApply(generation, paths, accepted => applied.Add(accepted.ToArray()));
        }
    }

    [TestMethod]
    public void TryApply_PreservesFilePathsAndIgnoresOnlyUnusablePaths()
    {
        var gate = new LatestInputGate();
        var generation = gate.BeginInput();
        IReadOnlyList<string>? applied = null;

        var didApply = gate.TryApply(
            generation,
            [@"C:\Folder", @"C:\notes.txt", "", "   ", null],
            paths => applied = paths);

        Assert.IsTrue(didApply);
        CollectionAssert.AreEqual(
            new[] { @"C:\Folder", @"C:\notes.txt" },
            applied!.ToArray());
    }
}
