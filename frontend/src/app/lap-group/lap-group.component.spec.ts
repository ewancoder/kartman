import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LapGroupComponent } from './lap-group.component';

describe('LapGroupComponent', () => {
  let component: LapGroupComponent;
  let fixture: ComponentFixture<LapGroupComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [LapGroupComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(LapGroupComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
