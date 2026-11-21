# Mobile layout update — 21 November 2026

The supplied desktop overview and placement captures show the existing table layout. The mobile screenshots show that hours and actions require horizontal scrolling.

This revision uses complete record cards at widths of 600 CSS pixels or less on the overview, placements, students and companies pages. Placement cards show student, status, position, company, dates, logged hours and the open action together. Directory cards show every existing field and action. Larger layouts keep their tables. The mobile and desktop representations bind to the same records and existing controller routes; no storage or workflow behavior changes.

The initial version built successfully on the user's Windows PC and all 25 regression groups passed, according to the supplied output. This revision has been checked statically; rerun the same checker for Razor compilation and regression validation. Review the updated mobile cards and navigation in a browser before publication. Menu expansion and placement-detail layout have not yet been visually verified from the supplied captures.
