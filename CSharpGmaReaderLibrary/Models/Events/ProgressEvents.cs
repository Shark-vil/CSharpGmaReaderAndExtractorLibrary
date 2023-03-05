namespace CSharpGmaReaderLibrary.Models.Events
{
    public sealed class ProgressEvents
    {
        public delegate void ProgressCompleted(object sender);
        public delegate void ProgressChanged(object sender, int percent);
    }
}
