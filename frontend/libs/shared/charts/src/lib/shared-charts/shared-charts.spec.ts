import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SharedCharts } from './shared-charts';

describe('SharedCharts', () => {
  let component: SharedCharts;
  let fixture: ComponentFixture<SharedCharts>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SharedCharts],
    }).compileComponents();

    fixture = TestBed.createComponent(SharedCharts);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
