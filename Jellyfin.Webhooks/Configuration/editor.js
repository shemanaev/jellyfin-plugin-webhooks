define(['loading', 'globalize', 'dom', 'dashboardcss', 'emby-input', 'emby-button', 'emby-select', 'emby-checkbox'], function (loading, globalize, dom) {
    'use strict';

    const pluginUniqueId = 'd8ca599b-ab3c-41b0-a4ea-6de1d52b9996';

    const FORMATS = [
        'Default',
        'Get',
        'Plex',
    ]

    const EVENTS = [
        'Play',
        'Pause',
        'Resume',
        'Stop',
        'Scrobble',
        'MarkPlayed',
        'MarkUnplayed',
        'Rate',
    ]

    function collectData(view) {
        const result = {};
        result.Id = view.querySelector('#hook-id').value;
        result.Url = view.querySelector('#text-url').value;
        result.Format = view.querySelector('#select-format').value;
        result.UserId = view.querySelector('#select-user').value;
        result.Events = Array.from(view.querySelectorAll('.events-container input[data-event]:checked'))
            .map(e => e.getAttribute('data-event'));
        return result;
    }

    function onSubmit(e) {
        const view = dom.parentWithClass(this, 'page')
        loading.show();

        const hook = collectData(view);

        ApiClient.ajax({
            url: ApiClient.getUrl('Webhooks'),
            type: 'POST',
            contentType: 'application/json',
            data: JSON.stringify(hook),
        }).then(() => {
            loading.hide();
            const page = Dashboard.getConfigurationPageUrl('Webhooks');
            Dashboard.navigate(page);
        });

        e.preventDefault();
        return false;
    }

    function getFormatHtml(format) {
        return '<option value="' + format + '">' + format + '</option>';
    }

    function getEventHtml(event) {
        let result = '';
        result += '<label>';
        result += '<input type="checkbox" is="emby-checkbox" data-event="' + event + '">';
        result += '<span>' + event + '</span>';
        result += '</label>';
        return result;
    }

    function getUserHtml(user) {
        return '<option value="' + user.Id + '">' + user.Name + '</option>';
    }

    function fillOptions(view) {
        view.querySelector('#select-format').innerHTML = FORMATS.map(getFormatHtml).join('');
        view.querySelector('.events-container').innerHTML = EVENTS.map(getEventHtml).join('');
        ApiClient.getUsers().then(users => {
            const defaultUsers = [{ Id: '', Name: globalize.translate('OptionAllUsers') }];
            view.querySelector('#select-user').innerHTML = defaultUsers.concat(users).map(getUserHtml).join('');
        });
    }

    function loadData(view, id) {
        if (!id) return;
        loading.show();
        ApiClient.getPluginConfiguration(pluginUniqueId).then(config => {
            const hook = config.Hooks.find(e => e.Id == id);
            if (hook) {
                view.querySelector('#hook-id').value = hook.Id;
                view.querySelector('#text-url').value = hook.Url;
                view.querySelector('#select-format').value = hook.Format;
                view.querySelector('#select-user').value = hook.UserId;
                hook.Events.forEach(e => view.querySelector('.events-container input[data-event="' + e + '"]').checked = true)
            }
            loading.hide();
        });
    }

    function onViewShow(params) {
        fillOptions(this);
        loadData(this, params.id);
    }

    return function (view, params) {
        view.addEventListener('viewshow', onViewShow.bind(view, params));
        view.querySelector('.form-webhook-editor').addEventListener('submit', onSubmit);
    };
});
