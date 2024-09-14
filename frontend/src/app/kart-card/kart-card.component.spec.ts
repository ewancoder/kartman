import { ComponentFixture, TestBed } from '@angular/core/testing';

import { KartCardComponent } from './kart-card.component';

describe('KartCardComponent', () => {
  let component: KartCardComponent;
  let fixture: ComponentFixture<KartCardComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [KartCardComponent]
    })
    .compileComponents();

    fixture = TestBed.createComponent(KartCardComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
