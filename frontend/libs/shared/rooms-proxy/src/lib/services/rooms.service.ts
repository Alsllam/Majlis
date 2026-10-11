import { Injectable, inject } from '@angular/core';
import { PagedResultDto, RestConfig, RestService } from '@majlis/core';
import { AddRoomParticipantDto, CreateRoomDto, FilterRoomDto, RoomDto, RoomIdDto, RoomListDto } from '../models/room.model';

/** Mirrors `RoomsAppService` (`[Route("rooms")]` on the Rooms host, `/api/rooms/rooms/...` through the BFF). */
@Injectable({ providedIn: 'root' })
export class RoomsService {
  private readonly rest = inject(RestService);
  readonly apiName = 'rooms';

  getList = (input: FilterRoomDto, config?: Partial<RestConfig>) =>
    this.rest.request<FilterRoomDto, PagedResultDto<RoomListDto>>(
      { method: 'POST', url: '/rooms/list', body: input },
      { apiName: this.apiName, ...config },
    );

  get = (id: string, config?: Partial<RestConfig>) =>
    this.rest.request<RoomIdDto, RoomDto>({ method: 'POST', url: '/rooms/getbyid', body: { id } }, { apiName: this.apiName, ...config });

  create = (input: CreateRoomDto, config?: Partial<RestConfig>) =>
    this.rest.request<CreateRoomDto, string>({ method: 'POST', url: '/rooms', body: input }, { apiName: this.apiName, ...config });

  addParticipant = (input: AddRoomParticipantDto, config?: Partial<RestConfig>) =>
    this.rest.request<AddRoomParticipantDto, void>(
      { method: 'POST', url: '/rooms/participants', body: input },
      { apiName: this.apiName, ...config },
    );
}
