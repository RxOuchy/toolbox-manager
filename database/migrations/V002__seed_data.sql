-- =========================================================================
-- V002 — seed data (development only)
-- =========================================================================
-- Registers the sample EchoTool so a fresh `docker compose up` boots into a
-- working end-to-end demo. Safe in dev because the path resolves only on the
-- machine running the polling service.
-- =========================================================================

INSERT INTO applications (id, name, description, executable_path, working_directory, timeout_seconds)
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'EchoTool',
    'Sample console app that echoes its arguments. Used to verify the toolbox end-to-end.',
    'C:\ToolboxApps\EchoTool\EchoTool.exe',
    'C:\ToolboxApps\EchoTool',
    60
)
ON CONFLICT (name) DO NOTHING;

INSERT INTO application_parameters
    (application_id, parameter_name, display_label, parameter_type, is_required, default_value, description, display_order)
VALUES
    ('11111111-1111-1111-1111-111111111111', '--message', 'Message',  'string',  true,  'hello', 'Text that will be echoed back.',                       1),
    ('11111111-1111-1111-1111-111111111111', '--count',   'Repeat',   'number',  false, '1',     'How many times to repeat the message.',                2),
    ('11111111-1111-1111-1111-111111111111', '--verbose', 'Verbose',  'flag',    false, NULL,    'Include extra diagnostic output.',                     3)
ON CONFLICT (application_id, parameter_name) DO NOTHING;
