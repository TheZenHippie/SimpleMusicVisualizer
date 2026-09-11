# Project Implementation Plan

## 1. Project High-Level Goals
- [ ] Task 1: Initialize architecture foundation
- [ ] Task 2: Implement core infrastructure & business logic
- [ ] Task 3: Develop user interface layer
- [ ] Task 4: Run comprehensive test suites & fix lint issues

## 2. Architecture Milestones
### Phase 1: Core Setup
- [ ] Establish project templates in VS 2026 Solution
- [ ] Implement central data models/DTOs and interfaces

### Phase 2: Application Core
- [ ] Build standalone domain engine / business logic layer
- [ ] Wire up dependency injection or configuration wiring (`appsettings.json`)

### Phase 3: Presentation Layer
- [ ] **Console:** Set up application entry-point and input parsing loop
- [ ] **WinForms:** Build forms, apply service layers, isolate control event logic
- [ ] **WPF:** Configure XAML views, bind properties to `ViewModelBase`, wire `RelayCommand` items

## 3. Risk Mitigation & Verification Points
- Ensure nullability checks clear compiler warning tolerances.
- Validate cross-thread calls in WinForms/WPF back to the main UI loop via `.Invoke`.
