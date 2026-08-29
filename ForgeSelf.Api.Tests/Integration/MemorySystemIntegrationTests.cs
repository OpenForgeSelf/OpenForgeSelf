using ForgeSelf.Api.Plugins.MemorySystem.Controllers;
using ForgeSelf.Api.Plugins.MemorySystem.Data;
using ForgeSelf.Api.Plugins.MemorySystem.Models;
using ForgeSelf.Api.Plugins.MemorySystem.Services;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace ForgeSelf.Api.Tests.Integration;

public class MemorySystemIntegrationTests
{
    private readonly Mock<IMemoryService> _mockMemoryService;
    private readonly MemoryController _controller;

    public MemorySystemIntegrationTests()
    {
        _mockMemoryService = new Mock<IMemoryService>();
        _controller = new MemoryController(_mockMemoryService.Object);
    }

    [Fact]
    public async Task Search_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new SearchMemoryRequest
        {
            Keyword = "test",
            Page = 1,
            PageSize = 20
        };
        var searchResult = new MemorySearchResult
        {
            Items = new List<MemoryDto>(),
            Total = 0,
            Page = 1,
            PageSize = 20
        };
        _mockMemoryService.Setup(s => s.SearchAsync(It.IsAny<SearchMemoryRequest>()))
            .ReturnsAsync(searchResult);

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_WithFilters_ReturnsOk()
    {
        // Arrange
        var request = new SearchMemoryRequest
        {
            Keyword = "test",
            Type = MemoryType.Fact,
            MinImportance = MemoryImportance.High,
            Page = 1,
            PageSize = 20
        };
        var searchResult = new MemorySearchResult
        {
            Items = new List<MemoryDto>
            {
                new() { Id = 1, Title = "Test Memory", Type = MemoryType.Fact, Importance = MemoryImportance.High }
            },
            Total = 1,
            Page = 1,
            PageSize = 20
        };
        _mockMemoryService.Setup(s => s.SearchAsync(It.IsAny<SearchMemoryRequest>()))
            .ReturnsAsync(searchResult);

        // Act
        var result = await _controller.Search(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemorySearchResult;
        response!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetById_ExistingId_ReturnsOk()
    {
        // Arrange
        var memory = new MemoryDto
        {
            Id = 1,
            Title = "Test Memory",
            Content = "Test content",
            Type = MemoryType.Fact
        };
        _mockMemoryService.Setup(s => s.GetByIdAsync(1))
            .ReturnsAsync(memory);
        _mockMemoryService.Setup(s => s.IncrementAccessAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.GetById(1);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryDto;
        response!.Title.Should().Be("Test Memory");
    }

    [Fact]
    public async Task GetById_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockMemoryService.Setup(s => s.GetByIdAsync(999))
            .ReturnsAsync((MemoryDto?)null);

        // Act
        var result = await _controller.GetById(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateMemoryRequest
        {
            Title = "New Memory",
            Content = "New content",
            Type = MemoryType.Fact
        };
        var createdMemory = new MemoryDto
        {
            Id = 1,
            Title = "New Memory",
            Content = "New content",
            Type = MemoryType.Fact
        };
        _mockMemoryService.Setup(s => s.CreateAsync(It.IsAny<CreateMemoryRequest>()))
            .ReturnsAsync(createdMemory);

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryDto;
        response!.Title.Should().Be("New Memory");
    }

    [Fact]
    public async Task Create_EmptyTitle_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMemoryRequest
        {
            Title = "",
            Content = "Some content"
        };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Create_EmptyContent_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMemoryRequest
        {
            Title = "Test Title",
            Content = ""
        };

        // Act
        var result = await _controller.Create(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMemoryRequest
        {
            Title = "Updated Memory"
        };
        var updatedMemory = new MemoryDto
        {
            Id = 1,
            Title = "Updated Memory"
        };
        _mockMemoryService.Setup(s => s.UpdateAsync(1, It.IsAny<UpdateMemoryRequest>()))
            .ReturnsAsync(updatedMemory);

        // Act
        var result = await _controller.Update(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryDto;
        response!.Title.Should().Be("Updated Memory");
    }

    [Fact]
    public async Task Update_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateMemoryRequest
        {
            Title = "Updated Memory"
        };
        _mockMemoryService.Setup(s => s.UpdateAsync(999, It.IsAny<UpdateMemoryRequest>()))
            .ReturnsAsync((MemoryDto?)null);

        // Act
        var result = await _controller.Update(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockMemoryService.Setup(s => s.DeleteAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.Delete(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Delete_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockMemoryService.Setup(s => s.DeleteAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.Delete(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        // Arrange
        var categories = new List<MemoryCategoryDto>
        {
            new() { Id = 1, Name = "Work" },
            new() { Id = 2, Name = "Personal" }
        };
        _mockMemoryService.Setup(s => s.GetCategoriesAsync())
            .ReturnsAsync(categories);

        // Act
        var result = await _controller.GetCategories();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as List<MemoryCategoryDto>;
        response.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateCategory_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateMemoryCategoryRequest
        {
            Name = "New Category"
        };
        var createdCategory = new MemoryCategoryDto
        {
            Id = 1,
            Name = "New Category"
        };
        _mockMemoryService.Setup(s => s.CreateCategoryAsync(It.IsAny<CreateMemoryCategoryRequest>()))
            .ReturnsAsync(createdCategory);

        // Act
        var result = await _controller.CreateCategory(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryCategoryDto;
        response!.Name.Should().Be("New Category");
    }

    [Fact]
    public async Task CreateCategory_EmptyName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateMemoryCategoryRequest
        {
            Name = ""
        };

        // Act
        var result = await _controller.CreateCategory(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCategory_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new UpdateMemoryCategoryRequest
        {
            Name = "Updated Category"
        };
        var updatedCategory = new MemoryCategoryDto
        {
            Id = 1,
            Name = "Updated Category"
        };
        _mockMemoryService.Setup(s => s.UpdateCategoryAsync(1, It.IsAny<UpdateMemoryCategoryRequest>()))
            .ReturnsAsync(updatedCategory);

        // Act
        var result = await _controller.UpdateCategory(1, request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task UpdateCategory_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        var request = new UpdateMemoryCategoryRequest
        {
            Name = "Updated Category"
        };
        _mockMemoryService.Setup(s => s.UpdateCategoryAsync(999, It.IsAny<UpdateMemoryCategoryRequest>()))
            .ReturnsAsync((MemoryCategoryDto?)null);

        // Act
        var result = await _controller.UpdateCategory(999, request);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteCategory_ExistingId_ReturnsOk()
    {
        // Arrange
        _mockMemoryService.Setup(s => s.DeleteCategoryAsync(1))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteCategory(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task DeleteCategory_NonExistingId_ReturnsNotFound()
    {
        // Arrange
        _mockMemoryService.Setup(s => s.DeleteCategoryAsync(999))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteCategory(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetStats_ReturnsOk()
    {
        // Arrange
        var stats = new MemoryStatsDto
        {
            TotalMemories = 10,
            TotalCategories = 3,
            TodayAccessed = 5
        };
        _mockMemoryService.Setup(s => s.GetStatsAsync())
            .ReturnsAsync(stats);

        // Act
        var result = await _controller.GetStats();

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as MemoryStatsDto;
        response!.TotalMemories.Should().Be(10);
    }

    [Fact]
    public async Task GetRelevantMemories_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new SearchMemoryRequest
        {
            Keyword = "test",
            PageSize = 5
        };
        var memories = new List<MemoryDto>
        {
            new() { Id = 1, Title = "Relevant Memory" }
        };
        _mockMemoryService.Setup(s => s.GetRelevantMemoriesAsync("test", 5, 0.1))
            .ReturnsAsync(memories);

        // Act
        var result = await _controller.GetRelevantMemories(request);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as List<MemoryDto>;
        response.Should().HaveCount(1);
    }

    [Fact]
    public async Task ImportMemories_ValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new ImportMemoryRequest
        {
            Items = new List<ImportMemoryItem>
            {
                new() { Title = "Import 1", Content = "Content 1" },
                new() { Title = "Import 2", Content = "Content 2" }
            }
        };
        _mockMemoryService.Setup(s => s.ImportMemoriesAsync(It.IsAny<ImportMemoryRequest>()))
            .ReturnsAsync(2);

        // Act
        var result = await _controller.ImportMemories(request);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ExportMemories_WithoutFilters_ReturnsOk()
    {
        // Arrange
        var memories = new List<MemoryDto>
        {
            new() { Id = 1, Title = "Exported Memory" }
        };
        _mockMemoryService.Setup(s => s.ExportMemoriesAsync(null, null))
            .ReturnsAsync(memories);

        // Act
        var result = await _controller.ExportMemories(null, null);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
        var okResult = result.Result as OkObjectResult;
        var response = okResult!.Value as List<MemoryDto>;
        response.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExportMemories_WithCategoryFilter_ReturnsOk()
    {
        // Arrange
        var memories = new List<MemoryDto>
        {
            new() { Id = 1, Title = "Work Memory", CategoryId = 1 }
        };
        _mockMemoryService.Setup(s => s.ExportMemoriesAsync(1, null))
            .ReturnsAsync(memories);

        // Act
        var result = await _controller.ExportMemories(1, null);

        // Assert
        result.Result.Should().BeOfType<OkObjectResult>();
    }
}
