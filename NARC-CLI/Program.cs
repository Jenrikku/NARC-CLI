using NARCSharp;
using NewGear.Trees.TrueTree;

bool forceNameless = false;
bool forceNoAlign = false;

string narcPath = string.Empty;
List<string> auxPaths = new();

// The program starts here:

if (args.Length == 0)
    Help();

for (int i = 1; i < args.Length; i++)
    ParseArgs(args, i);

ParseArgs(args, 0);

static void Help()
{
    Console.WriteLine(
        @"Usage: narc [OPTION] [FILE] [OTHER_FILES]

    Examples:
        narc x file.narc
        narc x file.narc output
        narc a file.narc file1 file2 folder ...
        narc c file.narc input
        narc c file.narc input --noalign --nameless
        narc d file.narc file1 file2 folder ...
        narc r file.narc name1 name2

    Available options:
        x           Extracts the contents of the NARC file.
        a           Adds files to a new or an existing NARC file.
        c           Creates a new NARC file from a given path.
        l           Lists the contents of the NARC file.
        d           Deletes files or folder in the NARC file.
        r           Rename a file in the NARC file.

    Other options:
        --help      Show help and exit.
        --nameless  Specify that the NARC contains no file names.
        --noalign   Force NARC to be written without alignment.
    Note that these may only be used after the NARC's path."
    );

    Environment.Exit(0);
}

void ParseArgs(string[] args, int currIdx)
{
    if (args.Length <= currIdx)
        return;

    switch (args[currIdx]) // Position independent
    {
        case "--help":
            Help();
            return;

        case "--nameless":
            forceNameless = true;
            return;

        case "--noalign":
            forceNoAlign = true;
            return;
    }

    switch (currIdx) // Position dependent
    {
        case 0:
            NARC narc;

            switch (args[currIdx])
            {
                case "x":
                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));

                    string output =
                        auxPaths.Count > 0 ? auxPaths[0] : Directory.GetCurrentDirectory();

                    Directory.CreateDirectory(output);
                    ExtractBranchNode(narc.RootNode, output);
                    return;

                case "a":
                    if (auxPaths.Count == 0)
                    {
                        Console.WriteLine("Input needed.");
                        Help();
                        return;
                    }

                    if (File.Exists(narcPath))
                        narc = NARCParser.Read(File.ReadAllBytes(narcPath));
                    else
                        narc = new();

                    if (forceNameless)
                        narc.Nameless = true;

                    if (forceNoAlign)
                        narc.HasAlignment = false;

                    foreach (string path in auxPaths)
                    {
                        string absPath = Path.GetFullPath(path);
                        AddPathToBranch(narc.RootNode, absPath, absPath);
                    }

                    File.WriteAllBytes(narcPath, NARCParser.Write(narc));
                    return;

                case "c":
                    if (auxPaths.Count == 0)
                    {
                        Console.WriteLine("Input needed.");
                        Help();
                        return;
                    }

                    string input = auxPaths[0];

                    if (!Directory.Exists(input))
                    {
                        Console.WriteLine($"\"{input}\" does not exist or is not a directory.");
                        return;
                    }

                    narc = new() { Nameless = forceNameless, HasAlignment = !forceNoAlign };

                    DirectoryInfo dir = new(input);

                    foreach (FileSystemInfo info in dir.EnumerateFileSystemInfos())
                        AddPathToBranch(narc.RootNode, input, info.FullName);

                    File.WriteAllBytes(narcPath, NARCParser.Write(narc));
                    return;

                case "l":
                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));

                    if (narc.Nameless)
                    {
                        Console.WriteLine("The NARC is nameless and has no file tree.");
                        Console.WriteLine($"It has a total of {narc.RootNode.Count()} files.");
                        return;
                    }

                    PrintBranch(narc.RootNode, 0);
                    return;

                case "d":
                    if (auxPaths.Count == 0)
                    {
                        Console.WriteLine("Input needed.");
                        Help();
                        return;
                    }

                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));

                    foreach (string path in auxPaths)
                    {
                        var root = narc.RootNode;

                        INode<byte[]>? child = root.FindChildByPath<LeafNode<byte[]>>(path);
                        child ??= root.FindChildByPath<BranchNode<byte[]>>(path);

                        switch (child)
                        {
                            case LeafNode<byte[]> leaf:
                                leaf.Parent?.RemoveChild(leaf);
                                break;

                            case BranchNode<byte[]> branch:
                                branch.Parent?.RemoveChild(branch);
                                break;

                            default:
                                Console.WriteLine($"\"{path}\" does not exist in the NARC file.");
                                break;
                        }
                    }

                    return;

                case "r":
                    if (auxPaths.Count < 2)
                    {
                        Console.WriteLine("Too few arguments.");
                        Help();
                        return;
                    }

                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));

                    {
                        var root = narc.RootNode;
                        var currBranch = root;

                        string pathIn = auxPaths[0];
                        string pathOut = auxPaths[1];

                        INode<byte[]>? child = root.FindChildByPath<LeafNode<byte[]>>(pathIn);
                        child ??= root.FindChildByPath<BranchNode<byte[]>>(pathIn);

                        if (child is null)
                        {
                            Console.WriteLine($"Node with name \"{pathIn}\" does not exist.");
                            return;
                        }

                        string[] pathTokens = pathOut.Split(
                            '/',
                            StringSplitOptions.RemoveEmptyEntries
                        );

                        for (int i = 0; i < pathTokens.Length - 1; i++)
                        {
                            string token = pathTokens[i];

                            var branch = currBranch.FindChildByPath<BranchNode<byte[]>>(token);

                            if (branch is null)
                            {
                                branch = new(token);
                                currBranch.AddChild(branch);
                            }

                            currBranch = branch;
                        }

                        child.Name = pathTokens[^1]; // Last element

                        if (child is LeafNode<byte[]> leaf)
                        {
                            leaf.Parent?.RemoveChild(leaf);
                            currBranch.AddChild(leaf);
                        }
                        else if (child is BranchNode<byte[]> branch)
                        {
                            branch.Parent?.RemoveChild(branch);
                            currBranch.AddChild(branch);
                        }
                    }

                    File.WriteAllBytes(narcPath, NARCParser.Write(narc));
                    return;

                default:
                    Help();
                    return;
            }

        case 1:
            narcPath = args[currIdx];
            return;
    }

    // Any other argument that does not match:
    auxPaths.Add(args[currIdx]);
}

void ExtractBranchNode(BranchNode<byte[]> node, string path)
{
    foreach (BranchNode<byte[]> branch in node.ChildBranches)
    {
        string newPath = Path.Join(path, branch.Name);
        Directory.CreateDirectory(newPath);
        ExtractBranchNode(branch, newPath);
    }

    foreach (LeafNode<byte[]> leaf in node.ChildLeaves)
    {
        string fileName = Path.Join(path, leaf.Name);
        File.WriteAllBytes(fileName, leaf.Contents ?? []);
    }
}

void AddPathToBranch(BranchNode<byte[]> node, string basePath, string path)
{
    string name = Path.GetFileName(path);
    string relativePath = Path.GetRelativePath(basePath, path);

    if (Directory.Exists(path))
    {
        DirectoryInfo dir = new(path);

        var branch = node.FindChildByPath<BranchNode<byte[]>>(relativePath);

        if (branch is null)
        {
            branch = new(name);
            node.AddChild(branch);
        }

        foreach (FileSystemInfo info in dir.EnumerateFileSystemInfos())
            AddPathToBranch(branch, basePath, info.FullName);

        return;
    }

    if (File.Exists(path))
    {
        byte[] bytes = File.ReadAllBytes(path);

        var leaf = node.FindChildByPath<LeafNode<byte[]>>(relativePath);

        if (leaf is null)
        {
            leaf = new(name);
            node.AddChild(leaf);
        }

        leaf.Contents = bytes;
        return;
    }

    // Path does not exist
    Console.WriteLine($"\"{path}\" does not exist.");
}

void PrintBranch(BranchNode<byte[]> node, int level)
{
    foreach (INode<byte[]> child in node)
    {
        for (int i = 0; i < level; i++)
            Console.Write('\t');

        Console.WriteLine(child.Name);

        if (child is BranchNode<byte[]> branch)
            PrintBranch(branch, level + 1);
    }
}
