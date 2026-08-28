using BeybladeMeta.Core.Ingestion;

namespace BeybladeMeta.Tests;

public class PaginationTests
{
    [Fact]
    public void Reads_last_page_from_pagination_bar()
    {
        const string html = """
            <div class="pagination">
              <a href="?page=151" class="pagination_previous">Previous</a>
              <a href="?page=1">1</a>
              <span class="pagination_current">152</span>
              <a href="?page=150">150</a>
            </div>
            """;

        Assert.Equal(152, MyBbPostExtractor.GetLastPageNumber(html));
    }

    [Fact]
    public void Single_page_thread_without_pagination_returns_one()
    {
        Assert.Equal(1, MyBbPostExtractor.GetLastPageNumber("<html><body>no pagination here</body></html>"));
    }
}
