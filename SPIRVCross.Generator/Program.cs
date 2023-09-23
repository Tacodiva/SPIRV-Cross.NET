using System;
using System.IO;
using Cake.Common;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Frosting;
using CppAst;

namespace SPIRVCross.Generator;

public static class Program {
    public static int Main(string[] args) {
        return new CakeHost()
            .UseContext<SPIRVCrossFrostingCtx>()
            .Run(args);
    }
}

public class SPIRVCrossFrostingCtx : FrostingContext {
    public string ProjectDirectory { get; set; }

    public SPIRVCrossFrostingCtx(ICakeContext context)
        : base(context) {
        ProjectDirectory = context.Argument("project_dir", "Vulkan");
    }
}

[TaskName("Default")]
public class DefaultTask : FrostingTask {
    public override void Run(ICakeContext context) {

        string outputPath = AppContext.BaseDirectory;

        if (!Path.IsPathRooted(outputPath)) {
            outputPath = Path.Combine(AppContext.BaseDirectory, outputPath);
        }

        if (!Directory.Exists(outputPath)) {
            Directory.CreateDirectory(outputPath);
        }

        string? headerFile = Path.Combine(AppContext.BaseDirectory, "spirv", "spirv_cross_c.h");
        var options = new CppParserOptions {
            ParseMacros = true,
        };

        var compilation = CppParser.ParseFile(headerFile, options);

        // Print diagnostic messages
        if (compilation.HasErrors) {
            foreach (var message in compilation.Diagnostics.Messages) {
                if (message.Type == CppLogMessageType.Error) {
                    var currentColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(message);
                    Console.ForegroundColor = currentColor;
                }
            }
        } else {
            CsCodeGenerator.Generate(compilation, outputPath);
        }
    }
}