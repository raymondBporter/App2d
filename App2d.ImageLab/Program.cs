namespace App2d.ImageLab;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args is ["--benchmark-edges", var benchmarkImage, var benchmarkOutput])
        {
            EdgeBenchmark.Run(benchmarkImage, benchmarkOutput);
            return 0;
        }
        if (args is ["--smoke-ui", var outputDirectory])
        {
            EditorSmoke.Run(outputDirectory);
            return 0;
        }
        if (args is ["--smoke-ui", var studyOutput, var studyImage])
        {
            EditorSmoke.Run(studyOutput, studyImage);
            return 0;
        }
        if (args is ["--study-edges", var sourcePath, var edgeOutput])
        {
            EdgeStudy.Run(sourcePath, edgeOutput);
            return 0;
        }
        using var window = new ImageLabWindow();
        if (args is [var imagePath])
        {
            window.LoadImage(imagePath);
        }
        else if (args.Length != 0)
        {
            MessageBox.Show("Usage: App2d.ImageLab [image-path | --smoke-ui output-directory [image-path] | --study-edges image-path output-directory | --benchmark-edges image-path output-json]", "Image Lab");
            return 1;
        }
        Application.Run(window);
        return 0;
    }
}
