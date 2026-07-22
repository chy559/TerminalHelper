using TerminalHelper.Windows.Input;

namespace TerminalHelper.WindowsPlatform.Tests.Input;

[TestClass]
public sealed class StartupPathReaderTests
{
    [TestMethod]
    public void Read_RemovesOnlyTheExecutableAndPreservesEveryRawArgument()
    {
        string[] arguments =
        [
            @"C:\Apps\TerminalHelper.Windows.exe",
            @"C:\Workspaces\one project",
            string.Empty,
            "  relative path  ",
            @"\\server\share\folder",
        ];

        CollectionAssert.AreEqual(arguments[1..], StartupPathReader.Read(arguments).ToArray());
    }

    [TestMethod]
    public void Read_ExecutableOnlyOrEmptyArgumentsProduceNoPaths()
    {
        Assert.IsEmpty(StartupPathReader.Read(["TerminalHelper.Windows.exe"]));
        Assert.IsEmpty(StartupPathReader.Read([]));
    }

    [TestMethod]
    public void Read_NullArgumentsThrows()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => StartupPathReader.Read(null!));
    }
}
