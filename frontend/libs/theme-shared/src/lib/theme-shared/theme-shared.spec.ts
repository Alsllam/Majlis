import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ThemeShared } from './theme-shared';

describe('ThemeShared', () => {
  let component: ThemeShared;
  let fixture: ComponentFixture<ThemeShared>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ThemeShared],
    }).compileComponents();

    fixture = TestBed.createComponent(ThemeShared);
    component = fixture.componentInstance;
    await fixture.whenStable();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
