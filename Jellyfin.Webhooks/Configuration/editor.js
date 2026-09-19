
const pluginUniqueId = 'd8ca599b-ab3c-41b0-a4ea-6de1d52b9996';

export function collectData(view) {
    const result = {};
    result.Id = view.querySelector('#hook-id').value;
    result.Name = view.querySelector('#text-name').value.trim();
    result.Url = view.querySelector('#text-url').value;
    result.Format = view.querySelector('#select-format').value;
    result.UserId = view.querySelector('#select-user').value;
    result.Events = Array.from(view.querySelectorAll('.events-container input[data-event]:checked'))
        .map(e => e.getAttribute('data-event'));
    return result;
}

export function resetEditor(view) {
    view.querySelector('.form-webhook-editor').reset();
    view.querySelector('#hook-id').value = '';
    view.querySelector('#text-name').value = '';
    view.querySelector('#text-url').value = '';
    view.querySelectorAll('.events-container input[data-event]')
        .forEach(input => input.checked = false);

    const format = view.querySelector('#select-format');
    const user = view.querySelector('#select-user');
    format.selectedIndex = format.options.length > 0 ? 0 : -1;
    user.selectedIndex = user.options.length > 0 ? 0 : -1;
}

export function scrollEditorToTop(view) {
    view.scrollTop = 0;
    if (typeof view.scrollTo === 'function') {
        view.scrollTo(0, 0);
    }
    if (typeof window !== 'undefined' && typeof window.scrollTo === 'function') {
        window.scrollTo(0, 0);
    }
}

function onSubmit(e) {
    const view = this.closest('.page')
    Dashboard.showLoadingMsg();

    const hook = collectData(view);

    ApiClient.ajax({
        url: ApiClient.getUrl('Webhooks'),
        type: 'POST',
        contentType: 'application/json',
        data: JSON.stringify(hook),
    }).then(() => {
        Dashboard.hideLoadingMsg();
        const page = Dashboard.getPluginUrl('Webhooks');
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
    return ApiClient.ajax({
        url: ApiClient.getUrl('Webhooks'),
        dataType: 'json'
    }).then(vars => {
        view.querySelector('#select-format').innerHTML = vars.formats.map(getFormatHtml).join('');
        view.querySelector('.events-container').innerHTML = vars.events.map(getEventHtml).join('');
    }).then(() => {
        return ApiClient.getUsers().then(users => {
            const defaultUsers = [{ Id: '', Name: 'All Users' }];
            view.querySelector('#select-user').innerHTML = defaultUsers.concat(users).map(getUserHtml).join('');
        });
    })
}

function loadData(view, id) {
    if (!id) return;
    return ApiClient.getPluginConfiguration(pluginUniqueId).then(config => {
        const hook = config.Hooks.find(e => e.Id == id);
        if (hook) {
            view.querySelector('#hook-id').value = hook.Id;
            view.querySelector('#text-name').value = hook.Name || '';
            view.querySelector('#text-url').value = hook.Url;
            view.querySelector('#select-format').value = hook.Format;
            view.querySelector('#select-user').value = hook.UserId;
            (hook.Events || []).forEach(e => view.querySelector('.events-container input[data-event="' + e + '"]').checked = true);
        }
    });
}

async function onViewShow(params) {
    Dashboard.showLoadingMsg();
    try {
        await fillOptions(this);
        resetEditor(this);
        await loadData(this, params.id);
        scrollEditorToTop(this);
    } finally {
        Dashboard.hideLoadingMsg();
    }
}

export default function (view, params) {
    view.addEventListener('viewshow', onViewShow.bind(view, params));
    view.querySelector('.form-webhook-editor').addEventListener('submit', onSubmit);
};
