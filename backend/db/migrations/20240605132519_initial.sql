-- migrate:up
create table "weather_history" (
    "id" bigserial primary key not null,
    "recorded_at" timestamp not null,
    "air_temp" decimal(8, 4) not null,
    "humidity" decimal (8, 4) not null,
    "precipitation" decimal(8, 4) not null,
    "cloud" decimal(8, 4) not null,
    "json_data" json not null /* TODO: Store more weather fields indexed in future. For now just gathering them all as json. */
);

create index "weather_history_recorded_at_idx" on "weather_history" ("recorded_at");

create table "weather" (
    "id" bigserial primary key not null,
    "recorded_at" timestamp not null,
    "weather_history_id" bigint null, /* If this is set - takes all values from there unless overridden. */
    /* The rest of the fields override weather_history when set */
    "air_temp" decimal(8, 4) null,
    "track_temp" decimal (8, 4) null,
    "track_temp_info" varchar(100) null,
    "humidity" decimal (8, 4) null,
    "humidity_info" varchar(100) null,
    "precipitation" decimal(8, 4) null,
    "precipitation_info" varchar(100) null,
    "cloud" decimal(8, 4) null,

    /* Additional fields for user input on the weather */
    "weather" smallint null,
    "sky" smallint null,
    "wind" smallint null,
    "track_temp_approximation" smallint null
);

create index "weather_recorded_at_idx" on "weather" ("recorded_at");
create index "weather_weather_history_id_idx" on "weather" ("weather_history_id");
create index "weather_air_temp_idx" on "weather" ("air_temp");

create table "session" (
    "id" varchar(36) primary key not null,
    "recorded_at" timestamp not null,
    "day" integer not null,
    "session" smallint not null,
    "total_length" varchar(20) not null,
    "weather_id" bigint not null,
    "track_config" varchar(40) null
);

create index "session_recorded_at_idx" on "session" ("recorded_at");
create index "session_day_idx" on "session" ("day");
create index "session_session_idx" on "session" ("session");
create index "session_total_length_idx" on "session" ("total_length");
create index "session_weather_id_idx" on "session" ("weather_id");
create index "session_track_config_idx" on "session" ("track_config");

create table "kart_data" (
    "id" bigserial primary key not null,
    "session_id" varchar(36) not null,
    "kart" varchar(20) not null,
    "driver_weight" decimal(8, 4) not null,
    "additional_weight" decimal(8, 4) not null,
    "fuel_start" decimal(8, 4) null, /* TODO: Consider adding a possibility to re-measure this at specific point of time (middle of the session). Both fuel and tires.*/
    "fuel_end" decimal(8, 4) null,
    "front_left_pressure_start" decimal(8, 4) null,
    "rear_left_pressure_start" decimal(8, 4) null,
    "front_right_pressure_start" decimal(8, 4) null,
    "rear_right_pressure_start" decimal(8, 4) null,
    "front_left_pressure_end" decimal(8, 4) null,
    "rear_left_pressure_end" decimal(8, 4) null,
    "front_right_pressure_end" decimal(8, 4) null,
    "rear_right_pressure_end" decimal(8, 4) null,
    "tires_heated" smallint null /* Get this automatically based on whether this cart was used in the previous session AND whether the time between sessions was short. 1 - cold, 2 - medium, 3 - heated */
);

create index "kart_data_session_id_idx" on "kart_data" ("session_id");
create index "kart_data_kart_idx" on "kart_data" ("kart");

create table "lap_data" (
    "id" bigserial primary key not null,
    "session_id" varchar(36) not null,
    "recorded_at" timestamp not null,
    "kart" varchar(20) not null,
    "lap" smallint not null,
    "laptime" decimal(8, 4) not null,
    "position" smallint not null,
    "gap" varchar(30) null,
    "weather_id" bigint null /* If it's not null - it sets the weather for the rest of laps of that session that have it null */
);

create unique index "lap_data_session_kart_lap" on "lap_data" ("session_id", "kart", "lap");
create index "lap_data_session_id_idx" on "lap_data" ("session_id");
create index "lap_data_recorded_at_idx" on "lap_data" ("recorded_at");
create index "lap_data_kart_idx" on "lap_data" ("kart");
create index "lap_data_lap_idx" on "lap_data" ("lap");
create index "lap_data_laptime_idx" on "lap_data" ("laptime");
create index "lap_data_position_idx" on "lap_data" ("position");
create index "lap_data_gap_idx" on "lap_data" ("gap");
create index "lap_data_weather_id_idx" on "lap_data" ("weather_id");

-- migrate:down
drop table "lap_data";
drop table "kart_data";
drop table "session";
drop table "weather";
drop table "weather_history";
