#nullable enable

namespace DataSaveManager.Tests
{
    internal sealed class TestIntWidget : DSMWidget<int>
    {
        protected override void Show(int value) { }

        public void CommitForTest(int value) => Commit(value);
    }
}
