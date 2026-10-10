import { ComponentFixture, TestBed } from '@angular/core/testing';
import { RoomsConfig } from './rooms-config';

describe('RoomsConfig', () => {
  let component: RoomsConfig;
  let fixture: ComponentFixture<RoomsConfig>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [RoomsConfig],
    }).compileComponents();

    fixture = TestBed.createComponent(RoomsConfig);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
