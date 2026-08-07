using Cake.Core.IO.Arguments;
using SPIRVCross.Generator;

string target = Argument("target", "Build");

bool buildLinux = Argument("build-linux", true);
bool buildWindows = Argument("build-windows", true);

string spirvCrossVersion = Argument("spirv-cross-version", "vulkan-sdk-1.4.357.0");
DirectoryPath spirvCrossPath = Argument("spirv-cross-path", "Native");
FilePath spirvCrossHeaderPath = Argument("spirv-cross-header-path", spirvCrossPath.CombineWithFilePath("spirv_cross_c.h"));
DirectoryPath spirvCrossOutput = Argument("output", "SPIRVCross");
FilePath mingwToolchainPath = Argument("mingw-toolchain", "SPIRVCross.Build/mingw-toolchain.cmake");

const string containerImageDefaultName = "spirv-cross-build";
string containerImage = Argument("container-image", "");
DirectoryPath containerSpirvcrossPath = Argument("container-spirv-cross-path", "/spirv-cross");
DirectoryPath containerBuildPath = Argument("container-spirv-cross-path", "/spirv-cross/build");

const string mingwToolchainDst = "ember-mingw-toolchain.cmake";

string[] sharedCMakeOptions = [
    "-D", "SPIRV_CROSS_STATIC=0",
    "-D", "SPIRV_CROSS_SHARED=1",
    "-D", "SPIRV_CROSS_CLI=0",
    "-D", "SPIRV_CROSS_ENABLE_TESTS=0",
    "-D", "SPIRV_CROSS_ENABLE_CPP=0",
    "-D", "SPIRV_CROSS_ENABLE_HLSL=0",
    "-D", "SPIRV_CROSS_ENABLE_MSL=0"
];

string[] linuxCMakeOptions = [
    ..sharedCMakeOptions,
];

string[] windowsCMakeOptions = [
    ..sharedCMakeOptions,

    "-D", $"CMAKE_TOOLCHAIN_FILE={mingwToolchainDst}"
];

Task("Clean")
    .Does(() => {
        DotNetClean(".");

        DeleteDirectory(spirvCrossPath, new() {
            Force = true,
            Recursive = true
        });
    });

Task("Clone")
    .Does(() => {
        if (!DirectoryExists(spirvCrossPath)) {
            Information("Cloning SPIRV-Cross...");
            GitClone("https://github.com/KhronosGroup/SPIRV-Cross.git", spirvCrossPath);
        } else {
            Information("Fetching SPIRV-Cross...");
            GitFetch(spirvCrossPath);
        }

        Information($"Checking out SPIRV-Cross {spirvCrossVersion}...");
        GitCheckout(spirvCrossPath, spirvCrossVersion);
    });

static string Escape(string arg) => new QuotedArgument(new TextArgument(arg)).Render();
static string EscapeDir(DirectoryPath arg) => Escape(arg.ToString());
static string EscapeFile(FilePath arg) => Escape(arg.ToString());

Task("Build")
    .IsDependentOn("Clone")
    .Does((ctx) => {

        if (string.IsNullOrEmpty(containerImage)) {
            containerImage = containerImageDefaultName;

            Information($"Building image {containerImage}...");

            DockerBuild(
                new() {
                    Tag = [containerImageDefaultName],
                },
                "."
            );
        }

        Information($"Running container with image {containerImage}...");

        DockerContainerRunSettings settings = new() {
            Detach = true,
            Interactive = true
        };
        string containerID = DockerRun(settings, containerImage, "/bin/bash");

        Information($"Started container {containerID}");

        try {
            DockerCp(EscapeDir(MakeAbsolute(spirvCrossPath)), $"{containerID}:{EscapeDir(containerSpirvcrossPath)}");

            void CheckPackages(params string[] packages) {
                DockerExec(containerID, "apt-get", [
                        "install", "-y", "--no-install-recommends", "--no-upgrade",
                        ..packages
                    ]
                );
            }

            Information($"Checking shared dependencies are present...");
            CheckPackages(
                "build-essential",
                "cmake"
            );

            if (buildLinux) {
                Information($"Making Linux build system...");
                DockerExec(containerID, "rm", "-rf", EscapeDir(containerBuildPath));
                DockerExec(containerID, "mkdir", "-p", EscapeDir(containerBuildPath));
                DockerExec(
                    containerID, "cmake",
                    args: [
                        "-S", EscapeDir(containerSpirvcrossPath),
                        "-B", EscapeDir(containerBuildPath),
                        .. linuxCMakeOptions
                    ]
                );

                Information($"Building for Linux...");
                DockerExec(
                    containerID, "cmake",
                    args: [
                        "--build", EscapeDir(containerBuildPath)
                    ]
                );

                CreateDirectory(spirvCrossOutput.Combine("runtimes/linux-x64/native"));
                DockerCp(
                    $"{containerID}:{EscapeFile(containerBuildPath.CombineWithFilePath("libspirv-cross-c-shared.so"))}",
                    EscapeDir(MakeAbsolute(spirvCrossOutput.Combine("runtimes/linux-x64/native"))),
                    new() {
                        FollowLink = true
                    }
                );
            }

            if (buildWindows) {
                Information($"Checking Windows dependencies are present...");
                CheckPackages("mingw-w64");

                DockerCp(EscapeFile(MakeAbsolute(mingwToolchainPath)), $"{containerID}:{EscapeFile(containerSpirvcrossPath.CombineWithFilePath(mingwToolchainDst))}");

                Information($"Making Windows build system...");
                DockerExec(containerID, "rm", "-rf", EscapeDir(containerBuildPath));
                DockerExec(containerID, "mkdir", "-p", EscapeDir(containerBuildPath));
                DockerExec(
                    containerID, "cmake",
                    args: [
                        "-S", EscapeDir(containerSpirvcrossPath),
                        "-B", EscapeDir(containerBuildPath),
                        .. windowsCMakeOptions
                    ]
                );

                Information($"Building for Windows...");
                DockerExec(
                    containerID, "cmake",
                    args: [
                        "--build", EscapeDir(containerBuildPath)
                    ]
                );

                CreateDirectory(spirvCrossOutput.Combine("runtimes/win-x64/native"));
                DockerCp(
                    $"{containerID}:{EscapeFile(containerBuildPath.CombineWithFilePath("libspirv-cross-c-shared.dll"))}",
                    EscapeDir(MakeAbsolute(spirvCrossOutput.Combine("runtimes/win-x64/native"))),
                    new() {
                        FollowLink = true
                    }
                );
            }
        } finally {
            Information($"Removing container...");
            DockerRm(new DockerContainerRmSettings() {
                Force = true
            }, containerID);
        }
    });

Task("SourceGen")
    .IsDependentOn("Clone")
    .Does(() => {
        DirectoryPath output = spirvCrossOutput.Combine("Generate");
        CleanDirectory(output);
        CsCodeGenerator.Generate($"{MakeAbsolute(spirvCrossHeaderPath)}", $"{MakeAbsolute(output)}");
    });

RunTarget(target);