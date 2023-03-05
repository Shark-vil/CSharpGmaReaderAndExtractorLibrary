namespace CSharpGmaReaderLibrary.Exceptions
{
    public class GmaReaderException : Exception
    {
        public GmaReaderException() { }

        public GmaReaderException(string message) : base(message) { }

        public GmaReaderException(string message, Exception inner) : base(message, inner) { }
    }
}
