import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SharedRealtime } from './shared-realtime';

describe('SharedRealtime', () => {
  let component: SharedRealtime;
  let fixture: ComponentFixture<SharedRealtime>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SharedRealtime],
    }).compileComponents();

    fixture = TestBed.createComponent(SharedRealtime);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
