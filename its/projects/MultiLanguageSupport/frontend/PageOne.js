import React from 'react';

class PageOne extends React.Component {
    constructor(props) {
        var message = 'Welcome to PageOne!'; // javascript:S3504
        super(props);
        this.state = {
            message: message,
        };
    }

    render() {
        return (
            <div>
                <h1>Page One</h1>
                <p>{this.state.message}</p>
            </div>
        );
    }
}

export default PageOne;