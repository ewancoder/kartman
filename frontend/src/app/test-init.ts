import { getTestBed } from '@angular/core/testing';
import { BrowserDynamicTestingModule, platformBrowserDynamicTesting } from '@angular/platform-browser-dynamic/testing';

export function tryInit() {
    try {
        getTestBed().initTestEnvironment(BrowserDynamicTestingModule, platformBrowserDynamicTesting());
    } catch {
        /* ng test will already have ran it, but wallaby needs it. */
    }
}
