import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StintSummaryComponent } from './stint-summary.component';

describe('StintSummaryComponent', () => {
    let component: StintSummaryComponent;
    let fixture: ComponentFixture<StintSummaryComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [StintSummaryComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(StintSummaryComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
