-- migrate:up
alter table "session"
add "updated_at" timestamp null;

-- migrate:down
alter table "session"
drop column "updated_at";
