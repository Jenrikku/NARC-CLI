using System.Runtime.InteropServices;
using NARCSharp;
using NewGear.Trees.TrueTree;

bool forceNameless = false;
bool forceNoAlign = false;
bool forceNoYaz0 = false;
bool useYaz0 = false;
bool nextArgIsYaz0Lvl = false;

string narcPath = string.Empty;
List<string> auxPaths = new();

int yaz0Lvl = 6;

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
        narc r file.narc input target_path
        narc d file.narc file1 file2 folder ...
        narc m file.narc name1 name2

    Available options:
        x               Extracts the contents of the NARC file.
        a               Adds files to a new or an existing NARC file.
        c               Creates a new NARC file from a given path.
        r               Replace a single file in the NARC with another one.
        l               Lists the contents of the NARC.
        d               Deletes files or folder in the NARC.
        m               Move a file in the NARC.

    Other options:
        --help          Show help and exit.
        --nameless      Specify that the NARC contains no file names.
        --noalign       Force NARC to be written without alignment.
        --yaz0 [lvl]    Use Yaz0 compression, optionally specify the level after.
        --noyaz0        Do not use Yaz0 when saving.
    Note that these may only be used after the NARC's path."
    );

    Environment.Exit(0);
}

void ParseArgs(string[] args, int currIdx)
{
    if (args.Length <= currIdx)
        return;

    if (nextArgIsYaz0Lvl && int.TryParse(args[currIdx], out int lvl))
    {
        yaz0Lvl = lvl;
        return;
    }

    nextArgIsYaz0Lvl = false;

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

        case "--yaz0":
            forceNoYaz0 = false;
            useYaz0 = true;
            nextArgIsYaz0Lvl = true;
            return;

        case "--noyaz0":
            forceNoYaz0 = true;
            useYaz0 = false;
            return;
    }

    switch (currIdx) // Position dependent
    {
        case 0:
            NARC narc;

            switch (args[currIdx])
            {
                case "x":
                    narc = ReadNARC(narcPath);

                    string output =
                        auxPaths.Count > 0 ? auxPaths[0] : Directory.GetCurrentDirectory();

                    output = Path.Join(output, Path.GetFileNameWithoutExtension(narcPath));

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
                        narc = ReadNARC(narcPath);
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

                    WriteNARC(narc, narcPath);
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

                    WriteNARC(narc, narcPath);
                    return;

                case "r":
                    if (auxPaths.Count < 2)
                    {
                        Console.WriteLine("Too few arguments.");
                        Help();
                        return;
                    }

                    if (File.Exists(narcPath))
                        narc = ReadNARC(narcPath);
                    else
                        narc = new();

                    if (forceNameless)
                        narc.Nameless = true;

                    if (forceNoAlign)
                        narc.HasAlignment = false;

                    {
                        var root = narc.RootNode;
                        var currBranch = root;

                        string inPath = auxPaths[0];
                        string targetPath = auxPaths[1];

                        if (!File.Exists(inPath))
                        {
                            Console.WriteLine($"\"{inPath}\" does not exist or is not a file.");
                            return;
                        }

                        byte[] bytes = File.ReadAllBytes(inPath);

                        string[] pathTokens = targetPath.Split(
                            '/',
                            StringSplitOptions.RemoveEmptyEntries
                        );

                        // Create branches if needed
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

                        string name = pathTokens[^1]; // Last element

                        var leaf = currBranch.FindChildByPath<LeafNode<byte[]>>(name);

                        if (leaf is not null)
                            leaf.Contents = bytes;
                        else
                        {
                            LeafNode<byte[]> newLeaf = new(name, bytes);
                            currBranch.AddChild(newLeaf);
                        }
                    }

                    WriteNARC(narc, narcPath);
                    return;

                case "l":
                    narc = ReadNARC(narcPath);

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

                    narc = ReadNARC(narcPath);

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

                    WriteNARC(narc, narcPath);
                    return;

                case "m":
                    if (auxPaths.Count < 2)
                    {
                        Console.WriteLine("Too few arguments.");
                        Help();
                        return;
                    }

                    narc = ReadNARC(narcPath);

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

                        // Create branches if needed
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

                            var childLeaf = currBranch.FindChildByPath<LeafNode<byte[]>>(leaf.Name);

                            if (childLeaf is not null)
                                currBranch.RemoveChild(childLeaf);

                            currBranch.AddChild(leaf);
                        }
                        else if (child is BranchNode<byte[]> branch)
                        {
                            branch.Parent?.RemoveChild(branch);

                            var childBranch = currBranch.FindChildByPath<BranchNode<byte[]>>(
                                branch.Name
                            );

                            if (childBranch is not null)
                                currBranch.RemoveChild(childBranch);

                            currBranch.AddChild(branch);
                        }
                    }

                    WriteNARC(narc, narcPath);
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

NARC ReadNARC(string path)
{
    byte[] bytes = File.ReadAllBytes(path);

    if (bytes[0] == 'Y' && bytes[1] == 'a' && bytes[2] == 'z' && bytes[3] == '0')
    {
        bytes = Yaz0Decompress(bytes);
        useYaz0 = !forceNoYaz0;
    }

    return NARCParser.Read(bytes);
}

void WriteNARC(NARC narc, string path)
{
    byte[] bytes = NARCParser.Write(narc);

    if (useYaz0)
        bytes = Yaz0Compress(bytes);

    File.WriteAllBytes(path, bytes);
}

unsafe byte[] Yaz0Compress(byte[] data)
{
    int maxBackLevel = (int)(0x10e0 * (yaz0Lvl / 9.0) - 0x0e0);

    byte* dataptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);

    byte[] result = new byte[data.Length + data.Length / 8 + 0x10];
    byte* resultptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(result, 0);
    *resultptr++ = (byte)'Y';
    *resultptr++ = (byte)'a';
    *resultptr++ = (byte)'z';
    *resultptr++ = (byte)'0';
    *resultptr++ = (byte)(data.Length >> 24 & 0xFF);
    *resultptr++ = (byte)(data.Length >> 16 & 0xFF);
    *resultptr++ = (byte)(data.Length >> 8 & 0xFF);
    *resultptr++ = (byte)(data.Length >> 0 & 0xFF);

    resultptr += 8;

    int length = data.Length;
    int dstoffs = 16;
    int Offs = 0;
    while (true)
    {
        int headeroffs = dstoffs++;
        resultptr++;
        byte header = 0;
        for (int i = 0; i < 8; i++)
        {
            int comp = 0;
            int back = 1;
            int nr = 2;
            {
                byte* ptr = dataptr - 1;
                int maxnum = 0x111;
                if (length - Offs < maxnum)
                    maxnum = length - Offs;
                //Use a smaller amount of bytes back to decrease time
                int maxback = maxBackLevel; //0x1000;
                if (Offs < maxback)
                    maxback = Offs;
                maxback = (int)dataptr - maxback;
                int tmpnr;
                while (maxback <= (int)ptr)
                {
                    if (*(ushort*)ptr == *(ushort*)dataptr && ptr[2] == dataptr[2])
                    {
                        tmpnr = 3;
                        while (tmpnr < maxnum && ptr[tmpnr] == dataptr[tmpnr])
                            tmpnr++;
                        if (tmpnr > nr)
                        {
                            if (Offs + tmpnr > length)
                            {
                                nr = length - Offs;
                                back = (int)(dataptr - ptr);
                                break;
                            }
                            nr = tmpnr;
                            back = (int)(dataptr - ptr);
                            if (nr == maxnum)
                                break;
                        }
                    }
                    --ptr;
                }
            }
            if (nr > 2)
            {
                Offs += nr;
                dataptr += nr;
                if (nr >= 0x12)
                {
                    *resultptr++ = (byte)(back - 1 >> 8 & 0xF);
                    *resultptr++ = (byte)(back - 1 & 0xFF);
                    *resultptr++ = (byte)(nr - 0x12 & 0xFF);
                    dstoffs += 3;
                }
                else
                {
                    *resultptr++ = (byte)(back - 1 >> 8 & 0xF | (nr - 2 & 0xF) << 4);
                    *resultptr++ = (byte)(back - 1 & 0xFF);
                    dstoffs += 2;
                }
                comp = 1;
            }
            else
            {
                *resultptr++ = *dataptr++;
                dstoffs++;
                Offs++;
            }
            header = (byte)(header << 1 | (comp == 1 ? 0 : 1));
            if (Offs >= length)
            {
                header = (byte)(header << 7 - i);
                break;
            }
        }
        result[headeroffs] = header;
        if (Offs >= length)
            break;
    }
    while (dstoffs % 4 != 0)
        dstoffs++;
    byte[] realresult = new byte[dstoffs];
    Array.Copy(result, realresult, dstoffs);
    return realresult;
}

static byte[] Yaz0Decompress(byte[] Data)
{
    uint leng = (uint)(Data[4] << 24 | Data[5] << 16 | Data[6] << 8 | Data[7]);
    byte[] Result = new byte[leng];
    int Offs = 16;
    int dstoffs = 0;
    while (true)
    {
        byte header = Data[Offs++];
        for (int i = 0; i < 8; i++)
        {
            if ((header & 0x80) != 0)
                Result[dstoffs++] = Data[Offs++];
            else
            {
                byte b = Data[Offs++];
                int offs = ((b & 0xF) << 8 | Data[Offs++]) + 1;
                int length = (b >> 4) + 2;
                if (length == 2)
                    length = Data[Offs++] + 0x12;
                for (int j = 0; j < length; j++)
                {
                    Result[dstoffs] = Result[dstoffs - offs];
                    dstoffs++;
                }
            }
            if (dstoffs >= leng)
                return Result;
            header <<= 1;
        }
    }
}
