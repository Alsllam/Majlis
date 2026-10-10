import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RoomsProxy } from './rooms-proxy';

describe('RoomsProxy', () => {
  let component: RoomsProxy;
  let fixture: ComponentFixture<RoomsProxy>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RoomsProxy],
    }).compileComponents();

    fixture = TestBed.createComponent(RoomsProxy);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
