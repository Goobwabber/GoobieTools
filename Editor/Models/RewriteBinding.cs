namespace GoobieTools.Editor.Models
{
    public class RewriteBinding
    {
        public string From { get; }
        public string To { get; }

        public RewriteBinding(string from, string to)
        {
            From = from;
            To = to;
        }
    }
}
