import { Injectable, inject } from '@angular/core';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import { CreateTaskDto, FilterTaskDto, SetTaskStatusDto, TaskDto, UpdateTaskDto } from './task.model';

/** Mirrors `TasksAppService` (`[Route("tasks")]`, reached as `/api/tasks/tasks/...`). */
@Injectable({ providedIn: 'root' })
export class TasksService {
  private readonly rest = inject(RestService);
  readonly apiName = 'tasks';

  private call<TBody, TResult>(method: 'POST' | 'PUT', url: string, body: TBody, config?: Partial<RestConfig>) {
    return this.rest.request<TBody, TResult>({ method, url: `/tasks${url}`, body }, { apiName: this.apiName, ...config });
  }

  getList = (input: FilterTaskDto, config?: Partial<RestConfig>) => this.call<FilterTaskDto, PagedResultDto<TaskDto>>('POST', '/list', input, config);
  get = (id: string, config?: Partial<RestConfig>) => this.call<{ id: string }, TaskDto>('POST', '/getbyid', { id }, config);
  create = (input: CreateTaskDto, config?: Partial<RestConfig>) => this.call<CreateTaskDto, string>('POST', '', input, config);
  update = (input: UpdateTaskDto, config?: Partial<RestConfig>) => this.call<UpdateTaskDto, void>('PUT', '', input, config);
  setStatus = (input: SetTaskStatusDto, config?: Partial<RestConfig>) => this.call<SetTaskStatusDto, void>('POST', '/status', input, config);
}
