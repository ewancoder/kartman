import { ComponentFixture, TestBed } from '@angular/core/testing';

import { KartInfoComponent } from './kart-info.component';

describe('KartInfoComponent', () => {
  let component: KartInfoComponent;
  let fixture: ComponentFixture<KartInfoComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KartInfoComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(KartInfoComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
