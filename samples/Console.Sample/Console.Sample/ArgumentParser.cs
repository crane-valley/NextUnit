namespace Console.Sample;

/// <summary>
/// Command-line argument parser.
/// </summary>
public class ArgumentParser
{
    /// <summary>
    /// Parses command-line arguments into an options object.
    /// </summary>
    public ParsedArguments Parse(string[] args)
    {
        var options = new ParsedArguments();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            switch (arg)
            {
                case "-h":
                case "--help":
                    options.ShowHelp = true;
                    break;

                case "-v":
                case "--verbose":
                    options.Verbose = true;
                    break;

                case "-o":
                case "--output":
                    if (i + 1 < args.Length)
                    {
                        options.OutputPath = args[++i];
                    }
                    else
                    {
                        options.Errors.Add("Missing value for --output");
                    }
                    break;

                case "-f":
                case "--format":
                    if (i + 1 < args.Length)
                    {
                        options.Format = args[++i];
                    }
                    else
                    {
                        options.Errors.Add("Missing value for --format");
                    }
                    break;

                default:
                    if (arg.StartsWith("-"))
                    {
                        options.Errors.Add($"Unknown option: {arg}");
                    }
                    else
                    {
                        options.InputFiles.Add(arg);
                    }
                    break;
            }
        }

        return options;
    }
}

/// <summary>
/// Parsed command-line arguments.
/// </summary>
public class ParsedArguments
{
    public bool ShowHelp { get; set; }
    public bool Verbose { get; set; }
    public string? OutputPath { get; set; }
    public string? Format { get; set; }
    public List<string> InputFiles { get; } = new();
    public List<string> Errors { get; } = new();
    public bool IsValid => Errors.Count == 0;
}
