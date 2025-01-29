using NARCSharp;
using NewGear.Trees.TrueTree;

bool forceNameless = false;
bool forceNoAlign = false;

string narcPath = string.Empty;
List<string> auxPaths = new();

// The program starts here:

if (args.Length < 0)
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
                        auxPaths.Count > 0 ? auxPaths[1] : Directory.GetCurrentDirectory();

                    ExtractBranchNode(narc.RootNode, output);
                    return;

                case "a":
                    if (auxPaths.Count == 0)
                    {
                        Console.WriteLine("Input needed.");
                        Help();
                        return;
                    }

                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));

                    if (forceNameless)
                        narc.Nameless = true;

                    if (forceNoAlign)
                        narc.HasAlignment = false;

                    foreach (string path in auxPaths)
                        AddPathToBranch(narc.RootNode, path);

                    File.WriteAllBytes(narcPath, NARCParser.Write(narc));
                    return;

                case "c":
                    if (auxPaths.Count == 0)
                    {
                        Console.WriteLine("Input needed.");
                        Help();
                        return;
                    }

                    string input = auxPaths[1];

                    narc = new() { Nameless = forceNameless, HasAlignment = !forceNoAlign };

                    AddPathToBranch(narc.RootNode, input);
                    File.WriteAllBytes(narcPath, NARCParser.Write(narc));
                    return;

                case "l":
                    narc = NARCParser.Read(File.ReadAllBytes(narcPath));
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

                    throw new NotImplementedException();

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
        ExtractBranchNode(branch, newPath);
    }

    foreach (LeafNode<byte[]> leaf in node.ChildLeaves)
    {
        string fileName = Path.Join(path, leaf.Name);
        File.WriteAllBytes(fileName, leaf.Contents ?? []);
    }
}

void AddPathToBranch(BranchNode<byte[]> node, string path)
{
    string name = Path.GetFileName(path);

    if (Directory.Exists(path))
    {
        DirectoryInfo dir = new(path);

        var branch = node.FindChildByPath<BranchNode<byte[]>>(path);
        branch ??= new(name);

        foreach (FileSystemInfo info in dir.EnumerateFileSystemInfos())
            AddPathToBranch(branch, info.FullName);

        return;
    }

    if (File.Exists(path))
    {
        byte[] bytes = File.ReadAllBytes(path);

        var leaf = node.FindChildByPath<LeafNode<byte[]>>(path);
        leaf ??= new(name);

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
