using MarketNest.Admin.Application;
using MarketNest.Admin.Domain;

namespace MarketNest.Admin.Infrastructure;

public class AnnouncementQuery(AdminReadDbContext db)
    : BaseQuery<Announcement, Guid>(db), IGetAnnouncementsPagedQuery, IGetActiveAnnouncementsQuery
{
    public async Task<PagedResult<AnnouncementDto>> ExecuteAsync(
        GetAnnouncementsPagedQuery request, CancellationToken ct)
    {
        var utcNow = DateTimeOffset.UtcNow;
        IQueryable<Announcement> query = Db.Announcements;

        if (!string.IsNullOrWhiteSpace(request.SearchTitle))
            query = query.Where(x => x.Title.Contains(request.SearchTitle));

        int total = await query.AsNoTracking().CountAsync(ct);

        List<AnnouncementDto> items = await query
            .AsNoTracking()
            .OrderByDescending(x => x.SortOrder)
            .ThenByDescending(x => x.StartDateUtc)
            .Skip(request.Skip)
            .Take(request.PageSize)
            .Select(x => new AnnouncementDto
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Type = x.Type,
                LinkUrl = x.LinkUrl,
                LinkText = x.LinkText,
                StartDateUtc = x.StartDateUtc,
                EndDateUtc = x.EndDateUtc,
                IsPublished = x.IsPublished,
                IsDismissible = x.IsDismissible,
                SortOrder = x.SortOrder,
                IsCurrentlyActive = x.IsPublished && x.StartDateUtc <= utcNow && x.EndDateUtc > utcNow
            }).ToListAsync(ct);

        return new PagedResult<AnnouncementDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }

    public async Task<IReadOnlyList<ActiveAnnouncementDto>> ExecuteAsync(CancellationToken ct)
    {
        var utcNow = DateTimeOffset.UtcNow;

        return await Db.Announcements
            .AsNoTracking()
            .Where(x => x.IsPublished && x.StartDateUtc <= utcNow && x.EndDateUtc > utcNow)
            .OrderByDescending(x => x.SortOrder)
            .ThenByDescending(x => x.StartDateUtc)
            .Select(x => new ActiveAnnouncementDto
            {
                Id = x.Id,
                Title = x.Title,
                Message = x.Message,
                Type = x.Type,
                LinkUrl = x.LinkUrl,
                LinkText = x.LinkText,
                IsDismissible = x.IsDismissible
            }).ToListAsync(ct);
    }
}
