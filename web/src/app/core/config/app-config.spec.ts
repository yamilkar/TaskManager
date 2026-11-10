import { loadAppConfig } from './app-config';

describe('loadAppConfig', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('fetches /config.json and returns the parsed config', async () => {
    const config = { usersApiUrl: 'http://u', tasksApiUrl: 'http://t' };
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: () => Promise.resolve(config) });
    vi.stubGlobal('fetch', fetchMock);

    await expect(loadAppConfig()).resolves.toEqual(config);
    expect(fetchMock).toHaveBeenCalledWith('/config.json');
  });

  it('rejects when the config cannot be loaded', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: false, status: 404 }));

    await expect(loadAppConfig()).rejects.toThrow('Failed to load /config.json (404)');
  });
});
