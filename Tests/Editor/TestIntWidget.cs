#nullable enable

namespace OperatorConsole.Tests
{
    internal sealed class TestIntWidget : OCWidget<int>
    {
        protected override void Show(int value) { }

        public void CommitForTest(int value) => Commit(value);
    }
}
