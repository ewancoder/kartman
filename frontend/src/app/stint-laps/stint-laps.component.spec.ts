import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StintLapsComponent } from './stint-laps.component';

describe('StintLapsComponent', () => {
    let component: StintLapsComponent;
    let fixture: ComponentFixture<StintLapsComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [StintLapsComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(StintLapsComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
