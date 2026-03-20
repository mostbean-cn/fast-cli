PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS command_groups (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS command_profiles (
    id TEXT PRIMARY KEY,
    group_id TEXT NOT NULL,
    name TEXT NOT NULL,
    description TEXT,
    working_directory TEXT,
    shell_type TEXT NOT NULL,
    run_mode TEXT NOT NULL,
    command_text TEXT NOT NULL,
    arguments_json TEXT NOT NULL DEFAULT '[]',
    environment_variables_json TEXT NOT NULL DEFAULT '[]',
    run_as_administrator INTEGER NOT NULL DEFAULT 0,
    sort_order INTEGER NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL,
    FOREIGN KEY (group_id) REFERENCES command_groups(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS execution_records (
    id TEXT PRIMARY KEY,
    command_profile_id TEXT NOT NULL,
    trigger_source TEXT NOT NULL,
    run_mode TEXT NOT NULL,
    status TEXT NOT NULL,
    started_at TEXT NOT NULL,
    ended_at TEXT,
    exit_code INTEGER,
    summary TEXT,
    output_text TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (command_profile_id) REFERENCES command_profiles(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_command_groups_sort_order
    ON command_groups(sort_order);

CREATE INDEX IF NOT EXISTS idx_command_profiles_group_sort
    ON command_profiles(group_id, sort_order);

CREATE INDEX IF NOT EXISTS idx_execution_records_profile_started
    ON execution_records(command_profile_id, started_at DESC);
