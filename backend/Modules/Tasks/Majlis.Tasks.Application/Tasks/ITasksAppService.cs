using Majlis.Framework.Application.Dtos;
using Majlis.Tasks.Application.Tasks.DTOs;

namespace Majlis.Tasks.Application.Tasks;

/// <summary>Tasks of a workspace (SRS §4.6). Agent-created tasks arrive through <c>ActionApproved</c>, never through this API.</summary>
public interface ITasksAppService
{
    Task<PagedResultDto<TaskDto>> GetListAsync(FilterTaskDto input, CancellationToken cancellationToken = default);

    Task<TaskDto> GetAsync(TaskIdDto input, CancellationToken cancellationToken = default);

    /// <summary>Creates a task by hand. Returns the new id.</summary>
    Task<Guid> CreateAsync(CreateTaskDto input, CancellationToken cancellationToken = default);

    Task UpdateAsync(UpdateTaskDto input, CancellationToken cancellationToken = default);

    Task SetStatusAsync(SetTaskStatusDto input, CancellationToken cancellationToken = default);
}
