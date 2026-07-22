using TerminalHelper.Core.Launching;
using TerminalHelper.WindowsPlatform.Discovery;
using TerminalHelper.WindowsPlatform.Launching;

namespace TerminalHelper.WindowsPlatform.Tests.Launching;

[TestClass]
public sealed class WindowsLaunchRequestFactoryTests
{
    private readonly WindowsLaunchRequestFactory factory = new();

    [TestMethod]
    public void Create_TerminalCreatesOneNewWindowRequestPerFolder()
    {
        var requests = factory.Create(
            new(WorkspaceTarget.Terminal, @"C:\\Tools\\wt.exe"),
            [@"C:\\A & B", @"D:\\项目 (2)"]);

        Assert.AreEqual(2, requests.Count);
        Assert.AreEqual(@"C:\\Tools\\wt.exe", requests[0].FileName);
        CollectionAssert.AreEqual(
            new[] { "-w", "new", "-d", @"C:\\A & B" },
            requests[0].Arguments.ToArray());
        CollectionAssert.AreEqual(
            new[] { "-w", "new", "-d", @"D:\\项目 (2)" },
            requests[1].Arguments.ToArray());
    }

    [TestMethod]
    public void Create_CodeCreatesOneNewWindowRequestWithAllFolders()
    {
        var requests = factory.Create(
            new(WorkspaceTarget.VisualStudioCode, @"C:\\VS Code\\Code.exe"),
            [@"C:\\A's Work", @"D:\\项目"]);

        Assert.AreEqual(1, requests.Count);
        var request = requests[0];
        Assert.AreEqual(@"C:\\VS Code\\Code.exe", request.FileName);
        CollectionAssert.AreEqual(
            new[] { "--new-window", @"C:\\A's Work", @"D:\\项目" },
            request.Arguments.ToArray());
    }

    [TestMethod]
    public void Create_IdeaCreatesOneRequestPerFolderInInputOrder()
    {
        var requests = factory.Create(
            new(WorkspaceTarget.IntelliJIdea, @"C:\\JetBrains\\idea64.exe"),
            [@"D:\\项目", @"C:\\A's Work"]);

        Assert.AreEqual(2, requests.Count);
        CollectionAssert.AreEqual(new[] { @"D:\\项目" }, requests[0].Arguments.ToArray());
        CollectionAssert.AreEqual(new[] { @"C:\\A's Work" }, requests[1].Arguments.ToArray());
    }

    [TestMethod]
    public void Create_RejectsEmptyFolder()
    {
        Assert.Throws<ArgumentException>(() => factory.Create(
            new(WorkspaceTarget.Terminal, @"C:\\Tools\\wt.exe"),
            [@"C:\\Valid", ""]));
    }

    [TestMethod]
    public void Create_PreservesSpecialFolderCharactersWithoutEmbeddedQuotes()
    {
        var folders = new[] { @"C:\\A & B", @"D:\\项目 (2)", @"E:\\A's Work" };

        var requests = factory.Create(
            new(WorkspaceTarget.VisualStudioCode, @"C:\\VS Code\\Code.exe"),
            folders);

        CollectionAssert.AreEqual(
            folders.Prepend("--new-window").ToArray(),
            requests.Single().Arguments.ToArray());
        Assert.IsFalse(requests.Single().Arguments.Any(argument =>
            argument.Length >= 2 && argument.StartsWith('"') && argument.EndsWith('"')));
    }

    [TestMethod]
    public void ProcessLaunchRequest_SnapshotsArguments()
    {
        var arguments = new[] { "--new-window", @"C:\\Original" };
        var request = new ProcessLaunchRequest(@"C:\\VS Code\\Code.exe", arguments);

        arguments[1] = @"C:\\Changed";

        CollectionAssert.AreEqual(
            new[] { "--new-window", @"C:\\Original" },
            request.Arguments.ToArray());
    }
}
