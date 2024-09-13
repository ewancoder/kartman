-- migrate:up
alter table "kart_data"
drop column "invalid_lap";

alter table "lap_data"
add "invalid_lap" boolean null;

-- migrate:down
alter table "lap_data"
drop column "invalid_lap";

alter table "kart_data"
add "invalid_lap" boolean null;
