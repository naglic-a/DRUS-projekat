create table if not exists drus_variables (
	id SERIAL PRIMARY KEY,           
    name VARCHAR(50) NOT NULL UNIQUE,
    description TEXT,
    unit VARCHAR(10),
    min_safe_value DECIMAL(10,2),
    max_safe_value DECIMAL(10,2)
);

create table if not exists drus_log (
	id BIGSERIAL primary key,
	var_id INTEGER not null,
	var_value DECIMAL(10,2) not null,
	log_timestamp TIMESTAMP default CURRENT_TIMESTAMP,
	
	constraint fk_variable_log
		foreign key(var_id)
		references drus_variables(id)
		on delete cascade -- if variable gets deleted, its logs get deleted too
);

create index idx_log_timestamp on drus_log(log_timestamp);
create index idx_log_var_id on drus_log(var_id);

create table if not exists drus_alarm (
	id SERIAL primary key,
	var_id INTEGER not null,
	alarm_type VARCHAR(20),
	alarm_severity VARCHAR(10),	
	triggered_value DECIMAL(10,2),
	message TEXT,
	alarm_timestamp TIMESTAMP default CURRENT_TIMESTAMP,
	
	constraint fk_variable_alarm
		foreign key(var_id)
		references drus_variables(id)
);