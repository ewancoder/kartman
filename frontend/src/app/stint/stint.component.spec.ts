import { ComponentFixture, TestBed } from '@angular/core/testing';

import { StintComponent } from './stint.component';

describe('StintComponent', () => {
    let component: StintComponent;
    let fixture: ComponentFixture<StintComponent>;

    beforeEach(async () => {
        await TestBed.configureTestingModule({
            imports: [StintComponent]
        }).compileComponents();

        fixture = TestBed.createComponent(StintComponent);
        component = fixture.componentInstance;
        fixture.detectChanges();
    });

    it('should create', () => {
        expect(component).toBeTruthy();
    });
});
