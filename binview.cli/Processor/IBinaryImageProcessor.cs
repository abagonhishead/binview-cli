namespace binview.cli.Processor
{
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBinaryImageProcessor
    {
        string InputPath { get; }

        string OutputPath { get; }

        long InputFileLength { get; }

        Task ProcessAsync(CancellationToken cancellationToken = default(CancellationToken));
    }
}
