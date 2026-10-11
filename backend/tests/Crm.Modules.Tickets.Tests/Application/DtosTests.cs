using Crm.Modules.Tickets.Domain;
using Crm.Modules.Tickets.Tests.Support;

namespace Crm.Modules.Tickets.Tests.Application;

public sealed class DtosTests
{
    [Fact]
    public void TheCategoryNameDtoCopiesBothSides()
    {
        TicketCategoryNameDto dto = TicketCategoryNameDto.FromDomain(TicketFixtures.Name("تقنية", "Technical"));

        Assert.Equal(new TicketCategoryNameDto("تقنية", "Technical"), dto);
    }

    [Fact]
    public void TheCategoryNameDtoRefusesNull()
    {
        Func<LocalizedText, TicketCategoryNameDto> map = TicketCategoryNameDto.FromDomain;

        NullArguments.AssertThrowsArgumentNull(map, (object?)null);
    }

    [Fact]
    public void TheCategoryDtoCopiesEveryField()
    {
        Category category = TicketFixtures.NewCategory(ar: "تقنية", en: "Technical", order: 7, visible: false);
        category.Deactivate();

        TicketCategoryDto dto = TicketCategoryDto.FromDomain(category);

        Assert.Equal(category.Id.Value, dto.Id);
        Assert.Equal(Ids.DepartmentA1.Value, dto.DepartmentId);
        Assert.Equal(new TicketCategoryNameDto("تقنية", "Technical"), dto.Name);
        Assert.Equal(7, dto.DisplayOrder);
        Assert.False(dto.IsCustomerVisible);
        Assert.False(dto.IsActive);
        Assert.Same(category.RowVersion, dto.RowVersion);
    }

    [Fact]
    public void TheCategoryDtoRefusesNull()
    {
        Func<Category, TicketCategoryDto> map = TicketCategoryDto.FromDomain;

        NullArguments.AssertThrowsArgumentNull(map, (object?)null);
    }

    [Fact]
    public void RecordsCompareByValue()
    {
        Assert.Equal(new TicketPersonDto(Ids.Agent1, "A"), new TicketPersonDto(Ids.Agent1, "A"));
        Assert.NotEqual(new TicketPersonDto(Ids.Agent1, "A"), new TicketPersonDto(Ids.Agent1, "B"));
        Assert.Equal(new TicketCustomerRefDto(Ids.Customer1, "C-1", "ع", "E"), new TicketCustomerRefDto(Ids.Customer1, "C-1", "ع", "E"));
        Assert.Equal(new TicketDepartmentRefDto(Ids.DepartmentA1.Value, "A1", "ع", "E"), new TicketDepartmentRefDto(Ids.DepartmentA1.Value, "A1", "ع", "E"));
        Assert.Equal(new TicketCategoryRefDto(Ids.Category1, new TicketCategoryNameDto("ع", "E"), true), new TicketCategoryRefDto(Ids.Category1, new TicketCategoryNameDto("ع", "E"), true));
    }

    [Fact]
    public void TheQueueCountsDtoCarriesThreeCounts()
    {
        var dto = new TicketQueueCountsDto(1, 2, 3);

        Assert.Equal((1, 2, 3), (dto.Mine, dto.Unassigned, dto.All));
        Assert.Equal(dto, new TicketQueueCountsDto(1, 2, 3));
        Assert.NotEqual(dto, new TicketQueueCountsDto(1, 2, 4));
    }

    [Fact]
    public void TheAgentDtoCarriesItsDepartments()
    {
        var dto = new TicketAgentDto(Ids.Agent1, "Agent", [Ids.DepartmentA1.Value, Ids.DepartmentA2.Value]);

        Assert.Equal([Ids.DepartmentA1.Value, Ids.DepartmentA2.Value], dto.DepartmentIds);
    }

    [Fact]
    public void ThePortalDtosCarryTheirFields()
    {
        var category = new PortalTicketCategoryDto(Ids.Category1, "ع", "E");
        var summary = new PortalTicketSummaryDto(Ids.Ticket1, "T-000001", "S", "Received", category, Clock.Now, Clock.Later(1));
        var message = new PortalThreadMessageDto(1, Ids.Ticket2, "Customer", true, "Hi", Clock.Now);
        var page = new PortalThreadPageDto([message], 1);

        Assert.Equal(category, summary.Category);
        Assert.Equal("Received", summary.Status);
        Assert.True(message.IsYou);
        Assert.Equal(1, Assert.Single(page.Items).Sequence);
        Assert.Equal(1, page.NextCursor);
    }
}
