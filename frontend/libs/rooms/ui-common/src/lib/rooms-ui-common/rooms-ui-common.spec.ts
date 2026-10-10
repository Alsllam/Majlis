import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RoomsUiCommon } from './rooms-ui-common';

describe('RoomsUiCommon', () => {
  let component: RoomsUiCommon;
  let fixture: ComponentFixture<RoomsUiCommon>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RoomsUiCommon],
    }).compileComponents();

    fixture = TestBed.createComponent(RoomsUiCommon);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
