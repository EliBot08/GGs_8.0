#nullable enable
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using GGs.ErrorLogViewer.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GGs.ErrorLogViewer.Tests.Services
{
    public class BookmarkServiceTests : IDisposable
    {
        private readonly BookmarkService _service;
        private readonly Mock<ILogger<BookmarkService>> _loggerMock;
        private readonly string _testFilePath;

        public BookmarkServiceTests()
        {
            _loggerMock = new Mock<ILogger<BookmarkService>>();
            _service = new BookmarkService(_loggerMock.Object);
            _testFilePath = Path.GetTempFileName();
        }

        [Fact]
        public void AddBookmark_Should_CreateBookmark_WithCorrectProperties()
        {
            // Arrange
            var logEntryId = 12345L;
            var name = "Test Bookmark";
            var notes = "Test notes";
            var color = "#FF0000";

            // Act
            var bookmark = _service.AddBookmark(logEntryId, name, notes, color);

            // Assert
            bookmark.Should().NotBeNull();
            bookmark.LogEntryId.Should().Be(logEntryId);
            bookmark.Name.Should().Be(name);
            bookmark.Notes.Should().Be(notes);
            bookmark.Color.Should().Be(color);
            bookmark.Id.Should().NotBeEmpty();
            _service.Bookmarks.Should().Contain(bookmark);
        }

        [Fact]
        public void AddTag_Should_CreateTag_WithCorrectProperties()
        {
            // Arrange
            var name = "Custom Tag";  // Use unique name to avoid default tag
            var color = "#FF0000";
            var icon = "⭐";

            // Act
            var tag = _service.AddTag(name, color, icon);

            // Assert
            tag.Should().NotBeNull();
            tag.Name.Should().Be(name);
            tag.Color.Should().Be(color);
            tag.Icon.Should().Be(icon);
            _service.Tags.Should().Contain(tag);
        }

        [Fact]
        public void AddTag_Should_ReturnExisting_WhenNameAlreadyExists()
        {
            // Arrange
            var name = "Test Tag";
            var tag1 = _service.AddTag(name);

            // Act
            var tag2 = _service.AddTag(name);

            // Assert
            tag1.Should().BeSameAs(tag2);
            _service.Tags.Count(t => t.Name == name).Should().Be(1);
        }

        [Fact]
        public void AssignTagToEntry_Should_CreateAssociation()
        {
            // Arrange
            var tag = _service.AddTag("Test Tag");
            var entryId = 123L;

            // Act
            _service.AssignTagToEntry(entryId, tag.Id);

            // Assert
            var tags = _service.GetTagsForEntry(entryId);
            tags.Should().Contain(tag);
            
            var entries = _service.GetEntriesForTag(tag.Id);
            entries.Should().Contain(entryId);
        }

        [Fact]
        public void RemoveTagFromEntry_Should_RemoveAssociation()
        {
            // Arrange
            var tag = _service.AddTag("Test Tag");
            var entryId = 123L;
            _service.AssignTagToEntry(entryId, tag.Id);

            // Act
            _service.RemoveTagFromEntry(entryId, tag.Id);

            // Assert
            var tags = _service.GetTagsForEntry(entryId);
            tags.Should().NotContain(tag);
        }

        [Fact]
        public void RemoveTag_Should_RemoveAllAssociations()
        {
            // Arrange
            var tag = _service.AddTag("Test Tag");
            var entryId1 = 123L;
            var entryId2 = 456L;
            _service.AssignTagToEntry(entryId1, tag.Id);
            _service.AssignTagToEntry(entryId2, tag.Id);

            // Act
            _service.RemoveTag(tag.Id);

            // Assert
            _service.Tags.Should().NotContain(tag);
            _service.GetTagsForEntry(entryId1).Should().BeEmpty();
            _service.GetTagsForEntry(entryId2).Should().BeEmpty();
        }

        [Fact]
        public void SaveToFile_And_LoadFromFile_Should_PreserveData()
        {
            // Arrange
            var bookmark = _service.AddBookmark(123L, "Test Bookmark");
            var tag = _service.AddTag("Test Tag");
            _service.AssignTagToEntry(456L, tag.Id);

            // Act
            _service.SaveToFile(_testFilePath);
            
            var newService = new BookmarkService(_loggerMock.Object);
            newService.LoadFromFile(_testFilePath);

            // Assert
            newService.Bookmarks.Should().HaveCount(_service.Bookmarks.Count);
            newService.Tags.Count().Should().BeGreaterThan(0); // Includes default tags
            newService.GetEntriesForTag(tag.Id).Should().Contain(456L);
        }

        public void Dispose()
        {
            if (File.Exists(_testFilePath))
            {
                File.Delete(_testFilePath);
            }
        }
    }
}
